from pathlib import Path
import json
p=Path('.tmp/cleanup-batch17/baseline.json');d=json.loads(p.read_text());f='src/views/画廊展厅/ArtworkRanking.vue';d[f]=Path(f).read_text(encoding='utf-8-sig');p.write_text(json.dumps(d),encoding='utf-8')
p=Path(f);p.write_text('''<script setup lang="ts">
defineProps<{ period: string }>();
defineEmits(['explore']);
</script>

<template>
  <section class="content-state" role="status">
    <h2>{{ period }}榜暂未开放</h2>
    <p>当前还没有可用的公开榜单数据。你仍可在全部作品中浏览、点赞和收藏。</p>
    <button @click="$emit('explore')">浏览全部作品</button>
  </section>
</template>
''',encoding='utf-8')
p=Path('src/views/画廊展厅/index.vue');s=p.read_text(encoding='utf-8');s=s.replace('<ArtworkRanking :period="currentTab" />','<ArtworkRanking :period="currentTab" @explore="switchMode(\'explore\')" />');p.write_text(s,encoding='utf-8')
p=Path('src/views/画廊展厅/ArtworkDetail.vue');s=p.read_text(encoding='utf-8');s=s.replace('  scrollbar-width: none;','').replace('.md-detail-mask::-webkit-scrollbar {\n  display: none;\n}','').replace('  padding: 0 40px;','  padding: 0 32px 60px;\n  box-sizing: border-box;').replace('  padding: 40px 0;','  padding: 20px 0;\n  flex-wrap: wrap;\n  gap: 10px;').replace('  margin-bottom: 80px;','  margin-bottom: 32px;').replace('  font-size: 1.25rem;\n  line-height: 2.2;','  font-size: 1rem;\n  line-height: 1.85;').replace('  margin-bottom: 60px;','  margin-bottom: 24px;')
s=s.replace('<article class="md-article">','<article class="md-article content-panel">').replace('    padding: 0 24px;','    padding: 0 16px 60px;')
p.write_text(s,encoding='utf-8')

p=Path('tests/fixtures/community-preview.cjs');s=p.read_text(encoding='utf-8-sig')
a=s.index("const image = ");b=s.index('function catalogItem',a)
s=s[:a]+'''const image = 'http://127.0.0.1:5193/fixture-image/0.svg';
const palettes = [['#e5d8c4','#5c756a','#324d4c'],['#e4dbe8','#9993ae','#665c80'],['#e3c9ad','#b3795e','#6e514c']];
for (let i = 0; i < 6; i++) {
  const id = String(i + 1), cover = `http://127.0.0.1:5193/fixture-image/${i}.svg`;
  const title = ['山间来信','暮色里的远行者','夏末的海岸','雨后的城市','林间小屋 · 色彩练习','群山之上'][i];
  artworks.set(id, { id: +id, title, description: '记录光影与色彩的一次练习。作品独立发布，也可以通过标签被找到。', tags: ['风景','原创'], author: { username: '社区画师' }, uploadAt: now, images: [{url: cover, isCover: true}], canEdit: true, canInteract: true, likesCount: i * 3, favoritesCount: i, commentsCount: 0 });
  items.push({id, type:'artwork',title,summary:'记录光影与色彩的一次练习。',author:'社区画师',tags:'风景,原创',updatedAt:now,coverUrl:cover});
}
for (let i = 0; i < 6; i++) {
  const id = 'reading-' + i, type = i % 2 ? 'post' : 'blog';
  const title = ['从一张草图开始：我的创作笔记','今天在山里看到的颜色','如何为故事建立一份资料索引','分享一份近期的练习记录','把灵感变成可以完成的作品','你通常在哪里记录突然出现的灵感？'][i];
  const text = '创作并不总是从完整的计划开始。有时是一束光、一句对话，或一次偶然的相遇。把这些片段记下来，再慢慢找到它们之间的联系。';
  const cover = `http://127.0.0.1:5193/fixture-image/${i}.svg`;
  published.set(id,{id,type,title,authorName:'社区创作者',publishedAt:now,resonance:i*2,excerpt:type==='blog'?JSON.stringify({text,coverUrl:i===0?cover:''}):text,extraData:JSON.stringify({cardCover:i===3?cover:''}),blocks:[{id:'p-one',type:'paragraph',data:JSON.stringify(paragraph(text)),sortOrder:0}],canEdit:false});
  items.push({id,type,title,summary:text,author:'社区创作者',tags:'创作,记录',updatedAt:now});
}
''' + s[b:]
s=s.replace("  if (url.pathname === '/api/Plugins')", """  if (url.pathname.startsWith('/fixture-image/')) {
    const i = parseInt(url.pathname.split('/').pop()) || 0, c = palettes[i % palettes.length];
    res.setHeader('Content-Type','image/svg+xml');
    return res.end(`<svg xmlns="http://www.w3.org/2000/svg" width="800" height="${i % 2 ? 700 : 540}"><rect width="800" height="800" fill="${c[0]}"/><circle cx="580" cy="150" r="65" fill="#fff7"/><path d="M0 500L240 150 450 450 650 220 800 500V800H0Z" fill="${c[1]}"/><path d="M0 560L180 350 400 580 590 420 800 600V800H0Z" fill="${c[2]}"/></svg>`);
  }
  else if (url.pathname === '/api/LingMaiPublish/public-stream') {
    const type=url.searchParams.get('type'), page=+(url.searchParams.get('page')||1), size=+(url.searchParams.get('pageSize')||20);
    data=[...published.values()].filter(p=>!type||p.type===type).slice((page-1)*size,page*size);
  }
  else if (url.pathname === '/api/Plugins')""")
s=s.replace('coverImageUrl: image, authorName: user.username','coverImageUrl: a.images[0].url, authorName: a.author.username')
s=s.replace("const rows = [...artworks.values()].map", "const rows = [...artworks.values()].filter(a=>JSON.stringify(a).includes(url.searchParams.get('search') || '')).map")
p.write_text(s,encoding='utf-8')
