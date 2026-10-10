"""Step 3. Render the chosen page at higher dpi, crop the photo (edge trim, or "auto" block detection for photos
pasted on letter pages), resize to max 1000 px and save JPEG at quality 80..72 so it stays under 150 KB.
Per-place overrides in picks.json: {"ref": {"page": 3, "mode": "auto", "box": [x0,y0,x1,y1 as fractions], "rot": 90, "pdf": "pdf/other.pdf"}}.
Usage: python crop.py [ref ...]   -> crops/{ref}.jpg, crops.json"""
import json,subprocess,os,sys
from PIL import Image
import numpy as np
os.makedirs('crops',exist_ok=True); os.makedirs('pages',exist_ok=True)
def longest_run(mask, min_gap=3):
    best=(0,0,0); s=None; gap=0
    for i,v in enumerate(list(mask)+[False]*(min_gap+1)):
        if v:
            if s is None: s=i
            gap=0; e=i
        elif s is not None:
            gap+=1
            if gap>min_gap:
                if e-s>best[0]: best=(e-s,s,e)
                s=None
    return best[1],best[2]
def render(pdf,page,dpi=150):
    import re
    o=subprocess.run(['pdfinfo','-f',str(page),'-l',str(page),pdf],capture_output=True,text=True).stdout
    m=re.search(r'Page\s+\d+ size:\s+([\d.]+) x ([\d.]+)',o)
    if m:
        longpt=max(float(m[1]),float(m[2]))
        dpi=int(min(300,max(150,1400*72/longpt)))
    out=f'pages/{os.path.basename(pdf)[:-4]}_{page}_{dpi}'
    if not os.path.exists(out+'.png'):
        subprocess.run(['pdftoppm','-r',str(dpi),'-png','-singlefile','-f',str(page),'-l',str(page),pdf,out],check=True,capture_output=True)
    return Image.open(out+'.png').convert('RGB')
def autocrop(im,thr=0.45):
    g=np.asarray(im.convert('L'),dtype=np.int16)
    nw=g<228
    r0,r1=longest_run(nw.mean(1)>thr, max(3,g.shape[0]//150))
    sub=nw[r0:r1+1]
    c0,c1=longest_run(sub.mean(0)>thr, max(3,g.shape[1]//150))
    # second pass with rows restricted to columns
    sub2=nw[:,c0:c1+1]
    r0,r1=longest_run(sub2.mean(1)>thr, max(3,g.shape[0]//150))
    box=[c0,r0,c1+1,r1+1]
    # trim dark/white border lines
    def edge_ok(v): m=v.mean(); return 35<m<235 and v.std()>8
    x0,y0,x1,y1=box
    for _ in range(40):
        ch=False
        if x1-x0>50 and not edge_ok(g[y0:y1,x0]): x0+=1; ch=True
        if x1-x0>50 and not edge_ok(g[y0:y1,x1-1]): x1-=1; ch=True
        if y1-y0>50 and not edge_ok(g[y0,x0:x1]): y0+=1; ch=True
        if y1-y0>50 and not edge_ok(g[y1-1,x0:x1]): y1-=1; ch=True
        if not ch: break
    # small inset to avoid fuzzy edges
    ins=3
    return (x0+ins,y0+ins,x1-ins,y1-ins)
def edgetrim(im):
    g=np.asarray(im.convert('L'),dtype=np.float32)
    x0,y0,x1,y1=0,0,g.shape[1],g.shape[0]
    def blank(v):
        m=v.mean(); sd=v.std()
        return m<45 or (m>238 and sd<9) or ((v>235).mean()>0.97)
    for _ in range(4000):
        ch=False
        if x1-x0>100 and blank(g[y0:y1,x0]): x0+=1; ch=True
        if x1-x0>100 and blank(g[y0:y1,x1-1]): x1-=1; ch=True
        if y1-y0>100 and blank(g[y0,x0:x1]): y0+=1; ch=True
        if y1-y0>100 and blank(g[y1-1,x0:x1]): y1-=1; ch=True
        if not ch: break
    ins=2
    return (x0+ins,y0+ins,x1-ins,y1-ins)
def save(im,box,path,rot=0):
    c0=im.crop(box)
    if rot: c0=c0.rotate(rot,expand=True)
    for md in (1000,900,800,700):
        c=c0.copy(); c.thumbnail((md,md),Image.LANCZOS)
        for q in (80,78,75,72):
            c.save(path,'JPEG',quality=q,optimize=True,progressive=True)
            if os.path.getsize(path)<=150_000: return c.size,os.path.getsize(path),q
    return c.size,os.path.getsize(path),q
if __name__=='__main__':
    c=json.load(open('classify.json'))
    picks=json.load(open('picks.json')) if os.path.exists('picks.json') else {}
    res={}
    refs=sys.argv[1:] or list(c)
    for ref in refs:
        v=c[ref]
        if not v or 'pages' not in v: continue
        pk=picks.get(ref,{})
        page=pk.get('page') or next((p for p,m,d in v['pages'] if m>0.5),None)
        if page is None: res[ref]=None; continue
        im=render(pk.get('pdf') or v['pdf'],page)
        box=pk.get('box')
        if box: box=[int(b*im.width) if i%2==0 else int(b*im.height) for i,b in enumerate(box)]
        elif pk.get('mode')=='auto' : box=autocrop(im)
        else: box=edgetrim(im)
        size,nb,q=save(im,box,f'crops/{ref}.jpg',pk.get('rot',0))
        res[ref]={'pdf':pk.get('pdf') or v['pdf'],'page':page,'box':box,'size':size,'bytes':nb,'q':q,'pagesize':im.size}
        print(ref,res[ref],flush=True)
    old=json.load(open('crops.json')) if os.path.exists('crops.json') else {}
    old.update(res); json.dump(old,open('crops.json','w'),indent=0)
