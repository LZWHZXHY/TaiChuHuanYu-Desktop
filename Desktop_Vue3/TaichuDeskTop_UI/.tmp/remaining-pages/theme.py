from pathlib import Path
import re, colorsys

folders=['活动中心','柴圈板块','太初俱乐部']
extra=[Path('src/views/知识库')/n for n in ['GraphControlPanel.vue','GraphDetailsPanel.vue','GraphFilterPanel.vue','KnowledgeGraph.vue']]
def convert_rule(m):
 selector, body=m[1],m[2]
 background=re.search(r'(?<![-\w])background(?:-color)?\s*:\s*([^;]+)',body)
 bg=background[1] if background else ''
 # Image overlays keep legible text; interactive surfaces follow semantic tokens.
 overlay=any(x in selector for x in ['cover','image','badge','tag']) and 'rgba(0' in bg
 def declaration(m):
  prop,value=m[1],m[2]
  if prop.startswith('--') or 'shadow' in prop or prop in ['filter','backdrop-filter']: return m[0]
  def color(m):
   original=m[0]; h={'white':'#ffffff','black':'#000000'}.get(original.lower(),original.lower())
   if not h.startswith('#'): return original
   h=h[1:]
   if len(h)==3:h=''.join(x*2 for x in h)
   if len(h)!=6:return original
   r,g,b=[int(h[i:i+2],16)/255 for i in (0,2,4)]
   hue,light,sat=colorsys.rgb_to_hls(r,g,b); hue*=360
   neutral=(max(r,g,b)-min(r,g,b)<.14)
   if neutral:
    if 'border' in prop or prop=='outline': token='border-line'
    elif prop in ['color','fill','stroke']:
     if min(r,g,b)>.94:
      token='on-overlay' if overlay else 'on-accent' if bg and ('#' in bg or 'gradient' in bg) and not any(x in bg for x in ['#fff','#f9','#f8','#faf']) else 'text-primary'
     else: token='text-primary' if light<.38 else 'text-secondary'
    elif 'background' in prop: token='paper-card' if light>.98 else 'bg-main' if light>.96 else 'bg-sub'
    else:return original
   else:
    family='danger' if hue<15 or hue>340 else 'warning' if hue<65 else 'success' if hue<165 else 'info' if hue<255 else 'accent'
    if 'background' in prop and light>.75: token=('accent-soft' if family=='accent' else family+'-soft')
    elif 'border' in prop and light>.7:token='border-line'
    else:token='accent-color' if family=='accent' else family
   return 'var(--'+token+')'
  value=re.sub(r'#[\da-fA-F]{3,8}\b|(?<![\w-])(?:white|black)\b',color,value)
  if 'background' in prop:
   value=re.sub(r'rgba\(255,\s*255,\s*255,\s*[.\d]+\)', 'var(--paper-card)',value)
   value=re.sub(r'rgba\(0,\s*0,\s*0,\s*([.\d]+)\)',lambda m:'var(--overlay)' if float(m[1])>=.2 else 'var(--bg-sub)',value)
   value=re.sub(r'rgba\((?:99,\s*102,\s*241|79,\s*70,\s*229|168,\s*85,\s*247),\s*[.\d]+\)','var(--accent-soft)',value)
  return prop+':'+value
 return selector+'{'+re.sub(r'([\w-]+)\s*:([^;{}]+)',declaration,body)+'}'
def css(s):return re.sub(r'([^{}]+)\{([^{}]*)\}',convert_rule,s)
files=[p for folder in folders for p in (Path('src/views')/folder).rglob('*.vue')]+extra
changed=[]
for p in files:
 s=p.read_text(encoding='utf-8-sig')
 n=re.sub(r'(<style[^>]*>)([\s\S]*?)(</style>)',lambda m:m[1]+css(m[2])+m[3],s)
 n=re.sub(r'(?<!:)style="([^"]*)"',lambda m:'style="'+css('x{'+m[1]+'}')[2:-1]+'"',n)
 if n!=s:p.write_text(n,encoding='utf-8');changed.append(str(p))
Path('.tmp/remaining-pages/palette-files.json').write_text(__import__('json').dumps(changed,ensure_ascii=False),encoding='utf-8')
print('Updated styles:',len(changed))
