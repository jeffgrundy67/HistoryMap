"""Step 4. Pull caption candidates (view direction, elevation, date) from the text layer of the photo page
and the page after it. Output is for a human to review: caption_cands.json.
Usage: python caption.py"""
import json,subprocess,re
c=json.load(open('classify.json')); cr=json.load(open('crops.json'))
top=[x['ref'] for x in json.load(open('places.json'))]
KW=re.compile(r'(?i)\b(view|fa[cç]ade|elevation|camera|looking|facing|front|rear|north|south|east|west)\b')
out={}
def txt(pdf,p):
    return subprocess.run(['pdftotext','-layout','-f',str(p),'-l',str(p),pdf,'-'],capture_output=True,text=True).stdout
for r in top:
    v=cr.get(r)
    if not v: continue
    pdf=c[r]['pdf']; p=v['page']; cands=[]
    for q in (p, p+1):
        if q>c[r]['n']: continue
        for line in txt(pdf,q).splitlines():
            for seg in re.split(r'\s{3,}',line.strip()):
                seg=seg.strip(' .:-')
                if 8<=len(seg)<=90 and KW.search(seg):
                    letters=sum(ch.isalpha() or ch==' ' for ch in seg)/len(seg)
                    words=re.findall(r'[A-Za-z]{2,}',seg)
                    if letters>0.85 and len(words)>=2: cands.append((q,seg))
    out[r]=cands[:4]
    print(r,p,cands[:4])
json.dump(out,open('caption_cands.json','w'),indent=0)
