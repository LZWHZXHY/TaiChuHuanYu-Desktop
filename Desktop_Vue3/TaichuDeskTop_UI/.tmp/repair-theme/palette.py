from pathlib import Path
import re,json
basePath=Path('.tmp/repair-theme/frontend-before.json'); base=json.loads(basePath.read_text(encoding='utf-8'))
extra=[Path('src/components/GlobalNotify.vue')]+list(Path('src/views/身份认证').rglob('*.vue'))+[Path('src/assets/styles/content.css')]
for p in extra: base.setdefault(str(p).replace('\\','/'),p.read_text(encoding='utf-8-sig'))
basePath.write_text(json.dumps(base,ensure_ascii=False),encoding='utf-8')
mapping={}
for token,colors in {
 '--text-primary':'#1f2328 #000 #1d1d1f #21262d #30363d #2f363d #24292f #e8eef4 #f2f6fa',
 '--text-secondary':'#8c959f #6e7781 #a1aebb #86868b #57606a #afb8c1 #8b9aab #6b7a8a #c0cbd6 #596673 #a0aab4 #888 #aaa #bbb #555 #9e9e9e #6c7a89 #b5b0aa',
 '--border-line':'#d0d7de #e1e4e8 #eaecef #ccc #d2d2d7 #f0f0f0 #d8d0c4 #eee #e8e0d8 #d8c8c0 #e8e2da',
 '--bg-sub':'#f0f2f5 #f1f1f1 #f2f2f2 #f5f5f7 #f6f8fa #f3f4f6 #f5f0eb #ede8e2 #f0ebe5 #f8f5f0 #f5ece8 #fcf6f4',
 '--paper-card':'#fff #ffffff #0e1720 #0e1620',
 '--accent-color':'#0066cc #0969da #ff6b1a #ff8c4a #ff7e32 #ffa266',
 '--success-soft':'#fafcf9 #dafbe1', '--success':'#2da44e #1a7f37 #4ecb71 #78d7ce',
 '--danger':'#cf222e #ff5050 #ff6b6b', '--accent-soft':'#ffebe9 #fff5f5 #fff3e0',
}.items():
 for color in colors.split(): mapping[color]='var('+token+')'
files=extra[:-1]+list(Path('src/views/推送首页/components').glob('*.vue'))+list(Path('src/views/太初俱乐部').rglob('*.vue'))+list(Path('src/views/柴圈板块').rglob('*.vue'))
for p in files:
 s=p.read_text(encoding='utf-8-sig')
 def convert(m):
  css=re.sub(r'#[0-9a-fA-F]{3,8}\b',lambda x:mapping.get(x[0].lower(),x[0]),m[0])
  css=re.sub(r'(background(?:-color)?\s*:\s*)rgba\(255,\s*255,\s*255,\s*0\.[67]\)',r'\1var(--overlay)',css)
  return css
 s=re.sub(r'<style\b[\s\S]*?</style>',convert,s)
 p.write_text(s,encoding='utf-8')
p=Path('src/assets/styles/content.css');s=p.read_text(encoding='utf-8-sig').replace('background: var(--accent-color); color: #fff;', 'background: var(--accent-color); color: var(--on-accent);');p.write_text(s,encoding='utf-8')
