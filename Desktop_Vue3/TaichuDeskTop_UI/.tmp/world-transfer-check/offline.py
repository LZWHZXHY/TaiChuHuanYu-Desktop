from pathlib import Path
from base64 import b64encode
p=Path('.tmp/world-transfer-check/print-document.html')
s=p.read_text(encoding='utf-8')
for name in ['landscape','portrait']:
 s=s.replace(f'http://localhost:5195/.tmp/world-transfer-check/{name}.png', 'data:image/png;base64,'+b64encode((p.parent/f'{name}.png').read_bytes()).decode())
(p.parent/'print-offline.html').write_text(s,encoding='utf-8')
