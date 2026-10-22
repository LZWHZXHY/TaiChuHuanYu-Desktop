from pathlib import Path
p=Path('src/views/太初随笔/index.vue');s=p.read_text(encoding='utf-8-sig')
s=s.replace('gap: 24px; align-items: start;', 'gap: 24px; align-items: stretch;')
s=s.replace('.stream-card { background:', '.stream-card { display: flex; background:')
s=s.replace('.card-cover { display: block; aspect-ratio: 21 / 9;', '.card-cover { display: block; width: 30%; flex-shrink: 0;')
s=s.replace('.card-content { padding: 24px; }', '.card-content { padding: 24px; min-width: 0; flex: 1; display: flex; flex-direction: column; }')
s=s.replace('margin-top: 24px; padding-top: 16px;', 'margin-top: auto; padding-top: 16px;')
s=s.replace('.card-excerpt { color:', '.card-excerpt { margin-bottom: 24px; color:')
s=s.replace('.card-content { padding: 20px; } }', '.card-content { padding: 20px; } .stream-card { display: block; } .card-cover { width: 100%; aspect-ratio: 21 / 9; } }')
s=s.replace("  if (more) loadingMore.value = true;\n  else { page = 1; posts.value = []; hasMore.value = true; loading.value = true; loadingMore.value = false; }", """  if (more) {
    loadingMore.value = true;
  } else {
    page = 1;
    posts.value = [];
    hasMore.value = true;
    loading.value = true;
    loadingMore.value = false;
  }""")
s=s.replace("    if (current === generation) { loading.value = false; loadingMore.value = false; }", """    if (current === generation) {
      loading.value = false;
      loadingMore.value = false;
    }""")
s=s.replace("      if ('text' in excerpt || 'coverUrl' in excerpt) { content = excerpt.text || ''; cardCover = excerpt.coverUrl || ''; }", """      if ('text' in excerpt || 'coverUrl' in excerpt) {
        content = excerpt.text || '';
        cardCover = excerpt.coverUrl || '';
      }""")
p.write_text(s,encoding='utf-8')
