"""Review helper: contact sheet of finished crops.
Usage: python sheet.py out.png [ref ...]"""
import json,sys,os
from PIL import Image,ImageDraw,ImageFont
top=[x['ref'] for x in json.load(open('places.json'))]
refs=sys.argv[2:] or [r for r in top if os.path.exists(f'crops/{r}.jpg')]
out=sys.argv[1]; T=260; cols=5
rows=(len(refs)+cols-1)//cols
sh=Image.new('RGB',(cols*T,rows*(T+18)),'white'); d=ImageDraw.Draw(sh)
for i,r in enumerate(refs):
    im=Image.open(f'crops/{r}.jpg'); im.thumbnail((T-6,T-6))
    x=(i%cols)*T; y=(i//cols)*(T+18)
    sh.paste(im,(x+3,y+18)); d.text((x+4,y+3),f'{top.index(r)+1}:{r}',fill='red')
sh.save(out)
