from pathlib import Path
p=Path('.tmp/world-roundtrip/actual-export.md')
s=p.read_text(encoding='utf-8').replace('## 原始正文标题','## 手动修改标题',1)
Path('.tmp/world-roundtrip/edited-export.md').write_text(s,encoding='utf-8')
