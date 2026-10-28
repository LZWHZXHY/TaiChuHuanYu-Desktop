from pathlib import Path
from PIL import Image,ImageOps,ImageDraw
paths=sorted(Path('.tmp/world-transfer-check').glob('final-page-*.png'))
canvas=Image.new('RGB',(4*300,3*430),'#ddd');d=ImageDraw.Draw(canvas)
for i,p in enumerate(paths):
 im=Image.open(p);im.thumbnail((290,400));x=(i%4)*300;y=(i//4)*430
 canvas.paste(im,(x+(300-im.width)//2,y+22));d.text((x+12,y+4),f'PAGE {i+1}',fill='black')
canvas.save('.tmp/world-transfer-check/final-contact-sheet.png')
