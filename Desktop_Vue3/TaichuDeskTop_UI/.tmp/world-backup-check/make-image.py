from PIL import Image
from pathlib import Path
import random
p=Path('.tmp/world-backup-check')
Image.frombytes('RGB',(720,720),random.Random(123).randbytes(720*720*3)).save(p/'original.png')
