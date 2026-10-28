from PIL import Image, ImageDraw
from pathlib import Path
root=Path('.tmp/world-transfer-check')
for name,size in [('landscape',(1200,600)),('portrait',(320,1800))]:
 im=Image.new('RGB',size,'#e5eff4');d=ImageDraw.Draw(im)
 for i,color in enumerate(['#3b657d','#da886a','#95a99b','#e3c57d']):
  y=i*size[1]//4;d.rectangle((0,y,size[0],(i+1)*size[1]//4),fill=color)
  d.text((20,y+20),f'{name.upper()} - SECTION {i+1}',fill='black',stroke_width=1)
 d.rectangle((4,4,size[0]-5,size[1]-5),outline='black',width=4)
 im.save(root/f'{name}.png')
