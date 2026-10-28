from pathlib import Path
p=Path('src/views/世界观管理/世界观组件/WorldTransfer.vue');s=p.read_text(encoding='utf-8');s=s.replace('ol{padding-left:22px}','ol{padding-left:22px;max-height:32vh;overflow:auto;overscroll-behavior:contain}');p.write_text(s,encoding='utf-8')
p=Path('package.json');s=p.read_text(encoding='utf-8');s=s.replace('"test:world-components":','"test:world-backup": "node --test tests/world-backup.test.mjs",\n    "test:world-components":');p.write_text(s,encoding='utf-8')
