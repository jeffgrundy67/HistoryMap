"""Step 2. Render each downloaded PDF at 30 dpi (thumbs/) and score every page for "photo-ness"
(midtone fraction, dense rows) so photo pages can be told apart from text, forms and maps. Writes classify.json.
Usage: python classify.py"""
import json,subprocess,os,glob,sys
from PIL import Image, ImageDraw
import numpy as np
from concurrent.futures import ThreadPoolExecutor
r=json.load(open('fetch.json'))
os.makedirs('thumbs',exist_ok=True)
def pick_pdf(ref):
    ps=[a for a in r[ref]['assets'] if a.get('path')]
    ph=[a for a in ps if 'photo' in a['filename'].lower()]
    return (ph or ps or [None])[0]
def score(img):
    a=np.asarray(img.convert('L'),dtype=np.uint8)
    h,w=a.shape
    mid=((a>25)&(a<225)).mean()
    # largest dense block: rows with >50% non-white
    nw=(a<235)
    rows=nw.mean(1); cols=nw.mean(0)
    dense=(rows>0.5).mean()
    return round(float(mid),3),round(float(dense),3)
def one(ref):
    a=pick_pdf(ref)
    if not a: return ref,None
    pdf=a['path']
    try: n=int([l for l in subprocess.run(['pdfinfo',pdf],capture_output=True,text=True).stdout.splitlines() if l.startswith('Pages')][0].split()[-1])
    except Exception as e: return ref,{'err':str(e)}
    d=f'thumbs/{ref}'; os.makedirs(d,exist_ok=True)
    if not glob.glob(d+'/p-*.png'):
        subprocess.run(['pdftoppm','-r','30','-gray','-png','-l',str(min(n,80)),pdf,d+'/p'],capture_output=True)
    pages=[]
    for f in sorted(glob.glob(d+'/p-*.png'),key=lambda x:int(x.rsplit('-',1)[1][:-4])):
        p=int(f.rsplit('-',1)[1][:-4]); im=Image.open(f)
        pages.append((p,)+score(im))
    return ref,{'pdf':pdf,'url':a['url'],'n':n,'pages':pages,'kind':a.get('kind','NPGallery')}
out={}
with ThreadPoolExecutor(8) as ex:
    for ref,v in ex.map(one,list(r)):
        out[ref]=v
json.dump(out,open('classify.json','w'),indent=0)
