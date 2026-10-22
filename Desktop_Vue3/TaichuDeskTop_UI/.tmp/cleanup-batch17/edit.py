from pathlib import Path
base=Path('.')
def edit(path, fn):
 p=base/path;p.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')

def nav(s, active):
 s=s.replace('<script setup lang="ts">','<script setup lang="ts">\nimport CommunityNav from \'@/components/CommunityNav.vue\';',1)
 marker={'gallery':'<article class="gallery-container content-page">','knowledge':'<section class="knowledge-home content-page">','wiki':'<main class="wiki-page content-page">'}[active]
 selection=':active="type === \'wiki\' ? \'wiki\' : \'knowledge\'"' if active=='knowledge' else 'active="'+active+'"'
 return s.replace(marker,marker+'\n    <CommunityNav '+selection+' />',1)

def gallery(s):
 s=nav(s,'gallery')
 start=s.index('        <div class="mode-selectors">');end=s.index('        </div>',start)+len('        </div>')
 modes=s[start:end].replace('class="mode-selectors"','class="content-filters" aria-label="画廊视图"').replace(':class="[\'mode-item\', { active: viewMode === \'explore\' }]"',':aria-pressed="viewMode === \'explore\'"').replace(':class="[\'mode-item\', { active: viewMode === \'ranking\' }]"',':aria-pressed="viewMode === \'ranking\'"').replace('>探索<','>全部作品<').replace('>荣誉<','>作品榜单<')
 s=s[:start]+s[end:]
 s=s.replace('<div class="nav-right content-actions"><RouterLink to="/knowledge">知识库</RouterLink>','<div class="nav-right content-actions">')
 s=s.replace('    <main class="gallery-view">','    <div class="section-toolbar">'+modes+'<span class="section-hint">发现社区创作者的作品</span></div>\n    <main class="gallery-view">')
 s=s.replace('<span \n                v-for="t','<button \n                v-for="t').replace(':class="{ active: currentTab === t }"',':aria-pressed="currentTab === t"').replace('>{{ t }}</span>','>{{ t }}榜</button>')
 s=s.replace('<nav class="time-filter">','<nav class="content-filters" aria-label="榜单周期">').replace('灵脉巅峰 /','作品榜单 /')
 # Remove obsolete selectors instead of layering overrides.
 start=s.index('.mode-selectors {');end=s.index('.view-header {',start);s=s[:start]+s[end:]
 start=s.index('.time-filter {');end=s.index('.fade-enter-active',start);s=s[:start]+s[end:]
 return s
edit('src/views/画廊展厅/index.vue', gallery)

def knowledge(s):
 s=nav(s,'knowledge')
 s=s.replace('<h1>知识库</h1>','<h1>{{ type === \'wiki\' ? \'公共 Wiki\' : \'知识库\' }}</h1>')
 s=s.replace('从文章、作品和设定中找到需要的资料，直接打开原内容。','{{ type === \'wiki\' ? \'一起完善公共知识，让每次修改都有据可查。\' : \'社区的公开资料，在这里查找、阅读与引用。\' }}')
 a=s.index('        <RouterLink :to="{ name: \'GalleryHome\'');b=s.index('        <RouterLink to="/codex/new"',a)
 s=s[:a]+s[b:]
 s=s.replace('<RouterLink to="/codex/new">','<RouterLink class="primary" to="/codex/new">')
 s=s.replace('<aside class="type-nav" aria-label="资料类型">','<aside class="type-nav" aria-label="资料类型">\n          <span class="nav-caption">浏览资料</span>')
 s=s.replace('class="type-label">{{ knowledgeTypes[item.type] }}','class="type-label content-badge">{{ knowledgeTypes[item.type] }}')
 # Keep creation entry points together below filtering, not scattered through header.
 s=s.replace('        </aside>','          <div class="browse-create"><span class="nav-caption">贡献内容</span><RouterLink to="/gallery?publish=1">发布画作 ↗</RouterLink><RouterLink to="/write/blog">写博客 ↗</RouterLink><RouterLink to="/write/post">发帖子 ↗</RouterLink></div>\n        </aside>',1)
 s=s.replace('grid-template-columns: 160px minmax(0, 1fr);','grid-template-columns: 188px minmax(0, 1fr);')
 s=s.replace('.content-card {\n  display:', '.content-card {\n  transition: border-color .2s;\n  display:')
 s=s.replace('  width: 112px;\n  height: 100px;','  width: 144px;\n  height: 112px;')
 s=s.replace('.type-nav button {\n  text-align: left;\n}', '.type-nav button { text-align: left; border-color: transparent; }\n.type-nav { padding: 12px; border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); }\n.nav-caption { font-size: .78rem; color: var(--text-secondary); padding: 6px 12px; }\n.browse-create { display: flex; flex-direction: column; gap: 10px; padding-top: 16px; margin-top: 8px; border-top: 1px solid var(--border-line); }\n.browse-create a { padding: 0 12px; font-size: .85rem; color: var(--text-secondary); text-decoration: none; }\n.content-card:hover, .content-card:focus-within { border-color: var(--accent-color); }')
 s=s.replace('.type-nav {\n  flex-direction: row;\n  overflow-x: auto;\n}', '.type-nav { flex-direction: row; flex-wrap: wrap; padding: 8px; gap: 4px; }\n.type-nav > .nav-caption { width: 100%; }\n.browse-create { width: 100%; flex-direction: row; flex-wrap: wrap; gap: 4px 12px; padding-top: 8px; }\n.browse-create .nav-caption { display: none; }\n.browse-create a { padding: 6px; }')
 return s
edit('src/views/知识库/index.vue',knowledge)

def wiki(s):
 s=nav(s,'wiki')
 s=s.replace('<p class="type-label">公共知识 · Wiki · 社区共同维护</p>','<p class="eyebrow">公共知识 · 社区共同维护</p>')
 s=s.replace('<section v-if="editing">','<section v-if="editing" class="wiki-editor content-panel">')
 s=s.replace('        <SpiritPreview :model-value="entry.content" />\n        <section class="history">','''        <nav class="section-toolbar" aria-label="词条视图">
          <div class="content-filters"><button :aria-pressed="pane === 'article'" @click="pane = 'article'">词条正文</button><button :aria-pressed="pane === 'history'" @click="pane = 'history'">修订记录 · {{ entry.revisionCount || 1 }}</button></div>
          <span class="section-hint">每个公开版本都可追溯</span>
        </nav>
        <article v-show="pane === 'article'" class="wiki-reading content-panel"><SpiritPreview :model-value="entry.content" /><footer class="wiki-reading-footer"><span>发现错误或想补充资料？</span><button @click="startEdit">参与编辑</button><button @click="pane = 'history'">查看修订记录</button></footer></article>
        <section v-show="pane === 'history'" class="history content-panel">''')
 s=s.replace('<section v-if="comparison" class="comparison">','<section v-if="comparison && pane === \'history\' && !editing" class="comparison content-panel">')
 s=s.replace('<p class="before">','<p class="before"><strong class="diff-label">修改前</strong>').replace('<p class="after">','<p class="after"><strong class="diff-label">修改后</strong>')
 s=s.replace("const editing = ref(false)","const pane = ref<'article' | 'history'>('article');\nconst editing = ref(false)")
 s=s.replace('    const current = ++version;','    pane.value = \'article\';\n    const current = ++version;',1)
 s=s.replace('  max-width: 900px;', '  max-width: 1200px;').replace('  margin: 24px auto 80px;', '  margin: 0 auto 40px;')
 s=s.replace('  border-radius: 10px;\n}', '  border-radius: 10px;\n  padding: 20px;\n}',1)
 s=s.replace('.history, .comparison {\n  border-top: 1px solid var(--border-line);\n  margin-top: 36px;\n  padding-top: 20px;\n}', '.history, .comparison { margin-top: 24px; }\n.wiki-reading { padding: 36px 48px; }\n.wiki-reading :deep(.spirit-preview-renderer) { max-width: 760px; margin: auto; font-size: 1.05rem; line-height: 1.9; }\n.wiki-reading-footer { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; border-top: 1px solid var(--border-line); margin-top: 32px; padding-top: 20px; color: var(--text-secondary); font-size: .9rem; }\n.diff-label { display: block; color: var(--text-secondary); font-size: .78rem; margin-bottom: 6px; }\n.tags span, .contributor { padding: 3px 10px; background: var(--paper-sub); border-radius: 6px; font-size: .85rem; }\n.history li { border-left: 2px solid var(--border-line); padding-left: 20px; }\n@media(max-width: 720px) { .wiki-reading { padding: 20px; } fieldset { padding: 12px; } }')
 return s
edit('src/views/灵脉百科/components/WikiDetail.vue',wiki)
