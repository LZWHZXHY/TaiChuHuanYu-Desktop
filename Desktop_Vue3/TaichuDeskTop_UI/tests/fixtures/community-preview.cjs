// Local, in-memory acceptance fixture. Never forwards requests to production services.
// Run: node tests/fixtures/community-preview.cjs
const http = require('node:http');
const user = { id: '11111111-1111-1111-1111-111111111111', username: '本地验收用户', email: 'preview@example.invalid', permissions: [] };
const now = new Date().toISOString();
const paragraph = text => ({ type: 'paragraph', content: [{ type: 'text', text }] });
const doc = { type: 'doc', content: [paragraph('公共知识由社区共同维护。')] };
const drafts = new Map(), published = new Map(), artworks = new Map();
const items = [{ id: 'wiki-one', type: 'wiki', title: '如何整理创作资料', summary: '共同维护的公共知识，保留每次修订。', author: '社区贡献者', tags: '创作,资料', updatedAt: now }];
let failSave = true, failResource = true, failUpload = true;
const image = 'http://127.0.0.1:5193/fixture-image/0.svg';
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
function catalogItem(note, id) { return { id, type: note.type, title: note.title, summary: '本地验收发布内容', author: user.username, tags: (note.tags || []).join(','), updatedAt: now }; }
const api = http.createServer(async (req, res) => {
  res.setHeader('Access-Control-Allow-Origin', 'http://127.0.0.1:5192');
  res.setHeader('Access-Control-Allow-Headers', 'authorization,content-type');
  res.setHeader('Access-Control-Allow-Methods', 'GET,POST,PUT,DELETE,OPTIONS');
  if (req.method === 'OPTIONS') return res.end();
  const url = new URL(req.url, 'http://localhost');
  const chunks = []; for await (const chunk of req) chunks.push(chunk);
  const bytes = Buffer.concat(chunks); let body = {};
  try { body = JSON.parse(bytes.toString()); } catch {}
  let data; const fail = message => { res.statusCode = 503; return { message }; };
  if (url.pathname.startsWith('/fixture-image/')) {
    const i = parseInt(url.pathname.split('/').pop()) || 0, c = palettes[i % palettes.length];
    res.setHeader('Content-Type','image/svg+xml');
    return res.end(`<svg xmlns="http://www.w3.org/2000/svg" width="800" height="${i % 2 ? 700 : 540}"><rect width="800" height="800" fill="${c[0]}"/><circle cx="580" cy="150" r="65" fill="#fff7"/><path d="M0 500L240 150 450 450 650 220 800 500V800H0Z" fill="${c[1]}"/><path d="M0 560L180 350 400 580 590 420 800 600V800H0Z" fill="${c[2]}"/></svg>`);
  }
  else if (url.pathname === '/api/LingMaiPublish/public-stream') {
    const type=url.searchParams.get('type'), page=+(url.searchParams.get('page')||1), size=+(url.searchParams.get('pageSize')||20);
    data=[...published.values()].filter(p=>!type||p.type===type).slice((page-1)*size,page*size);
  }
  else if (url.pathname === '/api/Plugins') data = [{ name: '知识库', url: '/knowledge' }, { name: '画廊展厅', url: '/gallery' }, { name: '太初随笔', url: '/stream' }];
  else if (url.pathname === '/api/User/me') data = user;
  else if (url.pathname === '/api/knowledge-base/catalog') {
    const type = url.searchParams.get('type'), q = url.searchParams.get('search') || '', page = +(url.searchParams.get('page') || 1);
    const result = items.filter(i => (!type || type === 'all' || i.type === type) && JSON.stringify(i).includes(q));
    data = { items: result.slice((page - 1) * 24, page * 24), total: result.length, page, pageSize: 24 };
  } else if (url.pathname === '/api/Resource/list') { data = failResource ? fail('模拟首次目录失败') : { success: true, data: [{ name: '创作资料/参考说明.txt', size: 120 }] }; failResource = false; }
  else if (url.pathname === '/api/Resource/download') data = { success: true, downloadUrl: 'http://127.0.0.1:5193/file.txt' };
  else if (url.pathname === '/file.txt') { res.setHeader('Content-Type', 'text/plain'); return res.end('Local acceptance fixture'); }
  else if (url.pathname === '/api/wiki/categories') data = [{ id: 1, name: '公共知识', parentId: null, sortOrder: 0 }];
  else if (url.pathname === '/api/wiki/article/wiki-one') data = { id: 'wiki-one', title: items[0].title, content: JSON.stringify(doc), tags: '创作,资料', currentRevisionId: 2, revisionCount: 2, publishedAt: now };
  else if (url.pathname.endsWith('/wiki-one/history')) data = { items: [{ id: 2, contributorName: '资料整理者', createdAt: now, editSummary: '补充共同维护说明' }], total: 2, page: 1, contributors: [{ id: user.id, name: '资料整理者' }] };
  else if (url.pathname.includes('/wiki-one/history/')) data = { id: 2, content: JSON.stringify(doc), previousContent: JSON.stringify({ type: 'doc', content: [paragraph('公共知识由社区维护。')] }) };
  else if (url.pathname === '/api/wiki/update') data = { revisionId: 3, message: '修订已提交，审核通过后公开生效' };
  else if (url.pathname === '/api/LingMai/spaces') data = [{ id: 'space-one', name: '创作草稿' }];
  else if (url.pathname === '/api/LingMai/all') data = [...drafts.values()];
  else if (url.pathname === '/api/LingMai/notes') { const id = 'draft-' + (drafts.size + 1); data = { ...body, id, blocks: [], isPublic: false }; drafts.set(id, data); }
  else if (url.pathname === '/api/LingMai/sync') {
    if (failSave) { data = fail('模拟首次保存失败，请重试'); failSave = false; }
    else { drafts.set(body.noteId, { ...drafts.get(body.noteId), ...body }); data = { success: true }; }
  } else if (url.pathname.startsWith('/api/LingMai/draft-')) data = drafts.get(url.pathname.split('/').pop());
  else if (/\/api\/LingMaiPublish\/notes\/.+\/publish/.test(url.pathname)) {
    const note = drafts.get(url.pathname.split('/')[4]), id = 'public-' + note.id;
    note.isPublic = true; published.set(id, { ...note, id, originalNoteId: note.id, canEdit: true, authorName: user.username, publishedAt: now });
    const existing = items.findIndex(i => i.id === id); if (existing >= 0) items.splice(existing, 1);
    items.push(catalogItem(note, id)); data = { success: true, publishedId: id };
  } else if (url.pathname.startsWith('/api/LingMaiPublish/blog/')) data = published.get(url.pathname.split('/').pop());
  else if (url.pathname === '/api/NewArtwork/direct-upload') {
    if (failUpload) { data = fail('模拟首次上传失败，请重试'); failUpload = false; }
    else {
      const form = await new Response(bytes, { headers: { 'Content-Type': req.headers['content-type'] } }).formData();
      const id = artworks.size + 1, title = String(form.get('title'));
      artworks.set(String(id), { id, title, description: form.get('description'), tags: form.getAll('tags'), author: { username: user.username }, uploadAt: now, images: [{ url: image, isCover: true }], canEdit: true, canInteract: true, likesCount: 0, favoritesCount: 0, commentsCount: 0 });
      items.push({ id: String(id), type: 'artwork', title, summary: String(form.get('description')), tags: form.getAll('tags').join(','), author: user.username, updatedAt: now, coverUrl: image });
      data = { success: true, artworkId: id, imagesCount: 1 };
    }
  } else if (/\/api\/Artwork\/\d+\/comments/.test(url.pathname)) data = { items: [], total: 0, hasMore: false };
  else if (/\/api\/Artwork\/\d+$/.test(url.pathname)) data = artworks.get(url.pathname.split('/').pop());
  else if (url.pathname === '/api/Artwork') { const rows = [...artworks.values()].filter(a=>JSON.stringify(a).includes(url.searchParams.get('search') || '')).map(a => ({ ...a, coverImageUrl: a.images[0].url, authorName: a.author.username, imageCount: 1 })); data = { data: rows, total: rows.length, hasMore: false }; }
  else { res.statusCode = 404; data = { message: '本地验收未配置此接口' }; }
  if (!data) { res.statusCode = 404; data = { message: '内容不存在' }; }
  res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify(data));
});
(async () => {
  const { createServer } = await import('vite');
  const server = await createServer({ server: { host: '127.0.0.1', port: 5192, strictPort: true }, define: { 'import.meta.env.VITE_API_BASE_URL': JSON.stringify('http://127.0.0.1:5193/api') }, plugins: [{ name: 'local-fixture-session', transformIndexHtml: html => html.replace('<head>', `<head><script>localStorage.setItem('token','fixture-token');localStorage.setItem('userInfo',${JSON.stringify(JSON.stringify(user))});</script>`) }] });
  api.listen(5193, '127.0.0.1'); await server.listen();
  console.log('Local fixture: http://127.0.0.1:5192/#/knowledge (first save/upload intentionally fails)');
})();
