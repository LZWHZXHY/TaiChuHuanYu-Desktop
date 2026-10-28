import json
from pathlib import Path
for name in ['package.json','package-lock.json']:
 p=Path(name);s=p.read_text(encoding='utf-8');d=json.loads(s)
 target=d['dependencies'] if name=='package.json' else d['packages']['']['dependencies']
 target['fflate']='^0.8.3'
 p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
