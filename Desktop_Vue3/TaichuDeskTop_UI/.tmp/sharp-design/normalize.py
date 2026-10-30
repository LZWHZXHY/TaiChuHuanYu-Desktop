from pathlib import Path
import re, json, hashlib

root = Path('src')
backup = Path('.tmp/sharp-design/before')
changes = []
excluded = []
radius = re.compile(r'(border(?:-(?:top|bottom)-(?:left|right))?-radius\s*:\s*)([^;}\n"\']+)')

def normalize_css(css):
    def replace(m):
        value = m[2].strip()
        # 圆形轨道、状态点、棋子等保留原几何形状；仅归一化面板圆角。
        if '%' in value or value in ('0', '0px', 'inherit', 'initial', 'unset'): return m[0]
        if re.fullmatch(r'[\d.\s]+(?:px|rem|em)(?:[\d.\s]*(?:px|rem|em)?)*\s*(?:!important)?', value) or re.fullmatch(r'var\(--(?:radius-[\w-]+|content-radius)\)', value):
            return m[1] + '0' + (' !important' if '!important' in value else '')
        return m[0]
    return radius.sub(replace, css)

for p in root.rglob('*'):
    if p.suffix not in ('.vue', '.css'): continue
    if any('俱乐部' in part for part in p.parts) or p.name == 'ClubCard.vue' or p.as_posix() in ('src/assets/styles/base.css','src/assets/styles/variables.css'):
        excluded.append(str(p)); continue
    original = p.read_text(encoding='utf-8-sig')
    if p.suffix == '.vue':
        updated = re.sub(r'(<style\b[^>]*>)([\s\S]*?)(</style>)', lambda m:m[1]+normalize_css(m[2])+m[3], original)
        # 模板中的静态内联样式；不修改脚本与数据结构。
        updated = re.sub(r'(?<!:)style="([^"\n]*)"', lambda m:'style="'+normalize_css(m[1])+'"', updated)
    else:
        updated = normalize_css(original)
    if updated != original:
        dest=backup/p; dest.parent.mkdir(parents=True,exist_ok=True)
        if not dest.exists(): dest.write_bytes(p.read_bytes())
        p.write_text(updated,encoding='utf-8')
        changes.append({'path':p.as_posix(),'changedDeclarations':sum(a!=b for a,b in zip(radius.findall(original),radius.findall(updated)))})

Path('.tmp/sharp-design/shape-changes.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'files':len(changes),'declarations':sum(x['changedDeclarations'] for x in changes),'excluded':len(excluded)},ensure_ascii=False))
