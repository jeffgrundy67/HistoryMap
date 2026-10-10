"""Review helper: contact sheet of all page thumbnails for a place, with scores (ps_{ref}.png).
Usage: python pagesheet.py ref [ref ...]"""
import json,sys,glob
from PIL import Image,ImageDraw
c=json.load(open('classify.json'))
for ref in sys.argv[1:]:
    v=c[ref]; fs=sorted(glob.glob(f'thumbs/{ref}/p-*.png'),key=lambda x:int(x.rsplit('-',1)[1][:-4]))
    T=170; cols=8; n=len(fs); rows=(n+cols-1)//cols
    sh=Image.new('RGB',(cols*T,rows*(T+14)),'white'); d=ImageDraw.Draw(sh)
    sc={p:(m,dd) for p,m,dd in v['pages']}
    for i,f in enumerate(fs):
        p=int(f.rsplit('-',1)[1][:-4]); im=Image.open(f).convert('RGB'); im.thumbnail((T-4,T-4))
        x=(i%cols)*T; y=(i//cols)*(T+14); sh.paste(im,(x+2,y+14)); d.text((x+3,y+1),f'p{p} {sc[p][0]}',fill='red')
    sh.save(f'ps_{ref}.png'); print(ref,n,v['n'])
