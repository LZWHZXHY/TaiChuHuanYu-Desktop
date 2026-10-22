from pathlib import Path
p=Path('src/views/知识库/index.vue');s=p.read_text(encoding='utf-8');a=s.index('.knowledge-home {');b=s.index('h1 {',a);s=s[:a]+s[b:];s=s.replace('<select :value="type"','<select aria-label="选择资料类型" :value="type"');p.write_text(s,encoding='utf-8')
