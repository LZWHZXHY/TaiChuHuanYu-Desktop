from pathlib import Path
p=Path('src/views/画廊展厅/ArtworkDetail.vue');s=p.read_text(encoding='utf-8').replace('  opacity: 0;\n  animation: fadeUp', '  opacity: 1;\n  animation: fadeUp');p.write_text(s,encoding='utf-8')
