from pathlib import Path
import json,difflib
before=json.loads(Path('.tmp/cleanup-batch17/baseline.json').read_text(encoding='utf-8'))
sums={}; details=[]
for f,old in before.items():
 p=Path(f);new=p.read_text(encoding='utf-8-sig') if p.exists() else ''
 if (old or '').replace('\r\n','\n')==new.replace('\r\n','\n'):continue
 a=(old or '').splitlines(); b=new.splitlines(); add=delete=0
 for kind,i,j,k,l in difflib.SequenceMatcher(None,a,b,autojunk=False).get_opcodes():
  if kind!='equal':delete+=j-i;add+=l-k
 group='source' if f.startswith('src/') else 'tests' if f.startswith('tests/') else 'docs'
 x=sums.setdefault(group,{'added':0,'deleted':0});x['added']+=add;x['deleted']+=delete
 details.append({'file':f,'added':add,'deleted':delete,'net':add-delete})
for x in sums.values():x['net']=x['added']-x['deleted']
print(json.dumps({'groups':sums,'files':details},ensure_ascii=False,indent=2))
Path('.tmp/cleanup-batch17/diff.txt').write_text('\n'.join(''.join(difflib.unified_diff((old or '').splitlines(True),Path(f).read_text(encoding='utf-8-sig').splitlines(True),fromfile=f,tofile=f)) for f,old in before.items() if Path(f).exists()),encoding='utf-8')
