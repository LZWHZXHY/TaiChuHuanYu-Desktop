from pypdf import PdfReader
from pathlib import Path
import json
r=PdfReader('.tmp/world-transfer-check/world-print-check.pdf')
report={'pages':len(r.pages),'pageDetails':[]}
for i,p in enumerate(r.pages):
 t=p.extract_text() or ''
 report['pageDetails'].append({'page':i+1,'chars':len(t),'images':len(p.images),'start':t[:80],'end':t[-60:]})
text='\n'.join(p.extract_text() or '' for p in r.pages)
report['checks']={s:s in text for s in ['世界观','雾海','龘','𠮷','第 36 段','记录 45','LongUnbrokenLine']}
Path('.tmp/world-transfer-check/pdf-text.txt').write_text(text,encoding='utf-8')
Path('.tmp/world-transfer-check/pdf-inspection.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
