from pathlib import Path
import json,difflib
base=json.loads(Path('.tmp/repair-theme/frontend-before.json').read_text(encoding='utf-8'))
for p in ['src/composables/useTheme.ts','tests/theme.test.mjs']:base[p]=''
source_add=source_del=test_add=test_del=0
changed=[]
for p,old in base.items():
 new=Path(p).read_text(encoding='utf-8-sig') if Path(p).exists() else ''
 if new==old:continue
 add=delete=0
 for tag,a,b,c,d in difflib.SequenceMatcher(None,old.splitlines(),new.splitlines()).get_opcodes():
  if tag in ['replace','delete']:delete+=b-a
  if tag in ['replace','insert']:add+=d-c
 if p.startswith('tests/'):
  test_add+=add;test_del+=delete
 else:source_add+=add;source_del+=delete
 changed.append({'file':p,'added':add,'deleted':delete})
for p in Path('.tmp/repair-theme/backend').glob('*.before'):
 new=p.with_suffix('').read_text(encoding='utf-8-sig');old=p.read_text(encoding='utf-8-sig');add=delete=0
 for tag,a,b,c,d in difflib.SequenceMatcher(None,old.splitlines(),new.splitlines()).get_opcodes():
  if tag in ['replace','delete']:delete+=b-a
  if tag in ['replace','insert']:add+=d-c
 if p.name.startswith(('TestContext','ContentFlowChecks')):test_add+=add;test_del+=delete
 else:source_add+=add;source_del+=delete
 changed.append({'file':'backend/'+p.with_suffix('').name,'added':add,'deleted':delete})
report={'source':{'added':source_add,'deleted':source_del,'net':source_add-source_del},'tests':{'added':test_add,'deleted':test_del,'net':test_add-test_del},'files':changed}
Path('.tmp/repair-theme/change-stats.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='files'}))
print('changed files',len(changed))
