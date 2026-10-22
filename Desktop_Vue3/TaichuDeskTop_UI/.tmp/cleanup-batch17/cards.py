from pathlib import Path
p=Path('src/views/画廊展厅/ArtworkInfiniteGrid.vue');s=p.read_text(encoding='utf-8-sig')
s=s.replace('class="masonry-item group" role="button" tabindex="0" @keydown.enter.self="$emit(\'on-click\', art.id)"\n          @click="$emit(\'on-click\', art.id)"','class="masonry-item"')
s=s.replace('<div class="item-visual">','<button class="item-visual" :aria-label="\'查看作品：\' + art.title" @click="$emit(\'on-click\', art.id)">')
s=s.replace('<img :src="getFullImageUrl(art.coverImageUrl) || \'/default-cover.jpg\'" class="item-image" loading="lazy" />','<img v-if="art.coverImageUrl && !failedImages.has(art.id)" :src="getFullImageUrl(art.coverImageUrl)" class="item-image" :alt="art.title" loading="lazy" @error="failedImages.add(art.id)" /><span v-else class="image-fallback">图片暂不可用 · 打开作品查看</span>')
s=s.replace('{{ art.imageCount }}P','{{ art.imageCount }} 张')
s=s.replace('          </div>\n          \n          <div class="item-details">','          </button>\n          \n          <div class="item-details">',1)
s=s.replace('<h4 class="item-title">{{ art.title }}</h4>','<h2 class="item-title"><button @click="$emit(\'on-click\', art.id)">{{ art.title }}</button></h2>')
s=s.replace("const error = ref('');", "const error = ref('');\nconst failedImages = ref(new Set<number>());")
s=s.replace('const columnCount = ref(5);','const columnCount = ref(3);').replace('if (width < 640) columnCount.value = 2;\n  else if (width < 1024) columnCount.value = 3;\n  else if (width < 1440) columnCount.value = 4;\n  else columnCount.value = 5;','if (width < 600) columnCount.value = 1;\n  else if (width < 1000) columnCount.value = 2;\n  else columnCount.value = 3;')
s=s.replace('generation++; artworks.value = [];','generation++; failedImages.value.clear(); artworks.value = [];')
s=s[:s.index('<style scoped>')]+'''<style scoped>
.artwork-masonry-wrapper { width: 100%; }
.masonry-grid { display: flex; gap: 24px; align-items: flex-start; }
.masonry-column { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 24px; }
.masonry-item { background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 12px; overflow: hidden; transition: border-color .2s; }
.masonry-item:hover, .masonry-item:focus-within { border-color: var(--accent-color); }
.item-visual { position: relative; display: block; width: 100%; padding: 0; border: 0; background: var(--paper-sub); cursor: pointer; }
.item-image { display: block; width: 100%; height: auto; }
.image-fallback { display: grid; place-items: center; min-height: 200px; padding: 20px; color: var(--text-secondary); font-size: .85rem; }
.item-overlay { position: absolute; right: 12px; bottom: 12px; pointer-events: none; }
.badge { background: #0009; color: #fff; font-size: .75rem; border-radius: 6px; padding: 4px 8px; }
.item-details { padding: 16px 18px; }
.item-title { margin: 0 0 12px; font-size: 1.1rem; line-height: 1.5; }
.item-title button { background: none; color: var(--text-primary); border: 0; padding: 0; text-align: left; font: inherit; font-weight: 600; overflow-wrap: anywhere; }
.item-footer { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 12px; }
.author-info { display: flex; align-items: center; gap: 8px; min-width: 0; color: var(--text-secondary); font-size: .82rem; overflow-wrap: anywhere; }
.avatar { width: 24px; height: 24px; object-fit: cover; border-radius: 50%; }
.status-bar { text-align: center; color: var(--text-secondary); padding: 32px 0; }
.status-bar button { color: var(--text-primary); background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 8px; padding: 8px 16px; }
@media(max-width: 720px) { .masonry-grid, .masonry-column { gap: 16px; } }
</style>
'''
p.write_text(s,encoding='utf-8')

# 阅读弹层统一工具栏、内容容器和对比度，保留正文/互动组件。
for name in ['太初随笔/PublicNoteDetail.vue','画廊展厅/ArtworkDetail.vue']:
 p=Path('src/views')/name;s=p.read_text(encoding='utf-8-sig')
 s=s.replace('class="md-viewport"','class="md-viewport content-page"').replace('class="md-controls"','class="md-controls content-actions"')
 s=s.replace('←</span> 返回画廊','←</span> 返回列表')
 s=s.replace('class="floating-close" @click','class="floating-close" aria-label="关闭作品详情" @click')
 # toolbar already exposes closing; remove redundant floating close that overlapped mobile controls.
 import re
 s=re.sub(r'\n    <button class="floating-close"[^\n]+\n','\n',s)
 s=re.sub(r'\.floating-close\s*\{[^}]*\}\s*', '',s)
 s=re.sub(r'\.floating-close:hover\s*\{[^}]*\}\s*', '',s)
 s=s.replace('background: var(--paper-bg); overflow-y: auto; scrollbar-width: none;', 'background: var(--paper-bg); overflow-y: auto;')
 s=s.replace('.md-detail-mask::-webkit-scrollbar { display: none; }','')
 s=s.replace('color: #1d1d1f;', 'color: var(--text-primary);')
 s=s.replace('EOF / THE END','阅读完毕').replace('落笔于太初之境 · 视界广场','喜欢这篇内容？可以复制引用链接分享给社区。')
 # These selectors intentionally replace old values in component styles.
 if 'PublicNoteDetail' in name:
  s=s.replace('class="md-wrapper" v-if="post"','class="md-wrapper content-panel" v-if="post"')
  s=s.replace('max-width: 800px;', 'max-width: 940px;').replace('padding: 0 24px;', 'padding: 0 24px 40px; box-sizing: border-box;')
  s=s.replace('.md-wrapper { padding: 40px 0 120px; }', '.md-wrapper { padding: 32px 40px; margin-top: 24px; }')
  s=s.replace('margin-top: 40px;', 'margin-top: 0;').replace('border-radius: 24px;', 'border-radius: 12px;')
  s=s.replace('.article-header { margin-bottom: 28px; margin-top: 12px; }','.article-header { margin-bottom: 28px; margin-top: 24px; }')
  s=s.replace('  </style>', '</style>')
  s=s.replace('</style>', '@media(max-width: 720px) { .md-viewport { padding: 0 16px 60px; } .md-wrapper { padding: 20px; } .metadata { font-size: .8rem; gap: 8px; } }\n</style>')
 p.write_text(s,encoding='utf-8')
