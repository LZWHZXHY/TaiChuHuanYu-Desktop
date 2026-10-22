from pathlib import Path
import re
folders=['太初协作','世界观管理','管理面板','个人中心','太初灵脉']
brand=set('#4f46e5 #6366f1 #4338ca #2563eb #0066cc #0055b3 #0969da #007aff #409eff'.split())
success=set('#16a34a #059669 #00875a #34c759 #22c55e'.split())
danger=set('#ff4757 #ff4d4f #d9363e #dc2626 #ef4444 #ff3b30 #cf222e #f56c6c'.split())
soft={'#f0fdf4':'success-soft','#e3fcef':'success-soft','#ecfdf5':'success-soft','#bbf7d0':'success-soft','#fef2f2':'danger-soft','#fecaca':'danger-soft','#fee2e2':'danger-soft','#fff7ed':'warning-soft','#fefce8':'warning-soft','#fde68a':'warning-soft','#fff4e5':'warning-soft','#eef2ff':'accent-soft','#e0e7ff':'accent-soft','#e5f0ff':'accent-soft','#eef7ff':'accent-soft'}
warning=set('#d97706 #c2410c #cf8a05 #cf5e00 #ff9500 #f59e0b'.split())
def rgb(h):
 h=h[1:];h=''.join(x*2 for x in h) if len(h)==3 else h
 return [int(h[i:i+2],16) for i in (0,2,4)]
def rule(m):
 selector,body=m.group(1),m.group(2)
 bg=re.search(r'(?<![\w-])background(?:-color)?\s*:\s*(#[\da-fA-F]{3,6}|white|black)\b',body)
 bgval=bg[1].lower() if bg else ''
 overlay=bool(re.search(r'background[^;]*rgba?\(0[, ]',body))
 def decl(d):
  prop,value=d[1],d[2]
  def color(c):
   h=c[0].lower();h={'white':'#ffffff','black':'#000000'}.get(h,h)
   if h in soft:return 'var(--'+soft[h]+')'
   if h in brand:return 'var(--accent-color)'
   if h in success:return 'var(--success)'
   if h in danger:return 'var(--danger)'
   if h in warning:return 'var(--warning)'
   if len(h) not in (4,7):return c[0]
   vals=rgb(h);avg=sum(vals)/3
   if max(vals)-min(vals)>28:return c[0]
   if 'shadow' in prop:return c[0]
   if 'border' in prop or prop=='outline-color':return 'var(--border-line)'
   if prop in ('color','fill','stroke','caret-color'):
    if avg>244:
     return 'var(--on-overlay)' if overlay else 'var(--on-accent)' if bgval in brand or bgval in danger or bgval in success else 'var(--paper-card)'
    return 'var(--text-primary)' if avg<85 else 'var(--text-secondary)'
   if 'background' in prop:
    return 'var(--paper-card)' if avg>250 else 'var(--bg-main)' if avg>246 else 'var(--bg-sub)' if avg>205 else 'var(--text-primary)' if avg<85 else 'var(--border-line)'
   return c[0]
  value=re.sub(r'#[\da-fA-F]{3,8}\b|\bwhite\b|\bblack\b',color,value)
  if prop.startswith('background'):
   value=re.sub(r'rgba\(255,\s*255,\s*255,\s*[.\d]+\)', 'var(--paper-card)',value)
   # Solid/semi-solid panels need surface contrast; masks stay translucent.
   value=re.sub(r'rgba\(0,\s*0,\s*0,\s*(0?\.\d+)\)',lambda x:'var(--overlay)' if float(x[1])>=.2 else 'var(--bg-sub)',value)
  return prop+':'+value
 return selector+'{'+re.sub(r'([\w-]+)\s*:([^;{}]+)',decl,body)+'}'
def css(s):return re.sub(r'([^{}]+)\{([^{}]*)\}',rule,s)
changed=[]
for folder in folders:
 for p in (Path('src/views')/folder).rglob('*'):
  if p.suffix not in ('.vue','.css'):continue
  s=p.read_text(encoding='utf-8-sig')
  new=css(s) if p.suffix=='.css' else re.sub(r'(<style[^>]*>)([\s\S]*?)(</style>)',lambda m:m[1]+css(m[2])+m[3],s)
  # Static inline styles are also UI, but never rewrite user data, scripts or chart/canvas content.
  if p.suffix=='.vue':new=re.sub(r'(?<!:)style="([^"]*)"',lambda m:'style="'+css('x{'+m[1]+'}')[2:-1]+'"',new)
  if new!=s:p.write_text(new,encoding='utf-8');changed.append(str(p))
print('Theme files updated:',len(changed))
Path('.tmp/deep-pages/palette-files.txt').write_text('\n'.join(changed),encoding='utf-8')
