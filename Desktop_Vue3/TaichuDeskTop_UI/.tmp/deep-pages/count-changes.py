from pathlib import Path
import json, difflib

baseline = json.loads(Path('.tmp/deep-pages/before.json').read_text(encoding='utf-8-sig'))
backend = Path('../../TaiChuBackend_V2/TaiChuWeb_V2/TaiChuWeb_V2')
for name, relative in [('ArtworkEngagementService', 'Services/Artwork/ArtworkEngagementService.cs'), ('UserController', 'Controllers/UserController.cs')]:
    baseline[str(backend / relative)] = Path(f'.tmp/deep-pages/backend/{name}.before.cs').read_text(encoding='utf-8-sig')
added = removed = 0
changed = []
for name, before in baseline.items():
    path = Path(name)
    if not path.exists():
        continue
    after = path.read_text(encoding='utf-8-sig')
    a = d = 0
    for line in difflib.ndiff(before.splitlines(), after.splitlines()):
        a += line.startswith('+ ')
        d += line.startswith('- ')
    if a or d:
        changed.append({'file': name, 'added': a, 'removed': d})
        added += a
        removed += d
result = {'files': len(changed), 'added': added, 'removed': removed, 'net': added - removed, 'changes': changed}
Path('.tmp/deep-pages/change-count.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({key: result[key] for key in ['files', 'added', 'removed', 'net']}))
