"""Review helper: stack several ps_{ref}.png page sheets into one image.
Usage: python stack.py out.png ref [ref ...]"""
import sys
from PIL import Image,ImageDraw
out=sys.argv[1]; ims=[]
for r in sys.argv[2:]:
    im=Image.open(f'ps_{r}.png'); lab=Image.new('RGB',(im.width,16),'yellow'); ImageDraw.Draw(lab).text((3,2),r,fill='black')
    ims+= [lab,im]
W=max(i.width for i in ims); H=sum(i.height for i in ims)
s=Image.new('RGB',(W,H),'white'); y=0
for i in ims: s.paste(i,(0,y)); y+=i.height
s.save(out); print(s.size)
