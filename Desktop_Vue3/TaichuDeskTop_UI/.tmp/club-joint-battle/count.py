import json, pathlib, difflib
root=pathlib.Path.cwd()
before=json.loads((root/'.tmp/club-joint-battle/before.json').read_text(encoding='utf-8-sig'))
rows=[]
def compare(label,old,new,kind):
    if old==new:return
    add=remove=0
    for tag,a,b,c,d in difflib.SequenceMatcher(None,old.splitlines(),new.splitlines(),autojunk=False).get_opcodes():
        if tag in ('delete','replace'):remove+=b-a
        if tag in ('insert','replace'):add+=d-c
    rows.append(dict(path=label,kind=kind,added=add,removed=remove,net=add-remove))
for p,old in before.items():
    file=root/p
    if file.exists(): compare(p,old,file.read_text(encoding='utf-8-sig'),'tests' if p.startswith('tests') else 'source')
staged=root/'.tmp/club-joint-battle/backend'
for file in staged.rglob('*.cs'):
    p=file.relative_to(staged)
    original=file.with_suffix('.cs.before')
    old=original.read_text(encoding='utf-8-sig') if original.exists() else ''
    actual=root/'../../TaiChuBackend_V2'/p
    compare('backend/'+str(p),old,actual.read_text(encoding='utf-8-sig'),'tests' if str(p).startswith('tests') else 'source')
result=dict(files=rows,totals={kind:{key:sum(row[key] for row in rows if row['kind']==kind) for key in ['added','removed','net']} for kind in ['source','tests']})
(root/'.tmp/club-joint-battle/change-count.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
