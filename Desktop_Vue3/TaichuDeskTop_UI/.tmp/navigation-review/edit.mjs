import fs from 'node:fs';
function edit(p,fn){fs.writeFileSync(p,fn(fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n')))}
for(const p of ['画廊展厅/index.vue','太初随笔/index.vue','太初随笔/ComposeView.vue','知识库/index.vue','灵脉百科/components/WikiDetail.vue'])edit('src/views/'+p,s=>s.replace(/^.*<CommunityNav[^\n]*\n/m,'').replace(/^import CommunityNav[^\n]*\n/m,''));
edit('src/views/知识库/index.vue',s=>s.replace(/^.*<div class="browse-create">.*\n/m,''));
edit('src/views/个人中心/index.vue',s=>s.replace('      <RouterLink class="btn-line" to="/write/blog">写博客</RouterLink>','').replace('<RouterLink :to="currentType.create">新建{{ currentType.label }} →</RouterLink>','').replace('<div class="stat-item"><span>当前等级</span>', '<div class="stat-item"><RouterLink v-if="router.hasRoute(\'交易行\')" :to="{ name: \'交易行\' }">经验兑换与配额 →</RouterLink><span>当前等级</span>'));
edit('src/views/太初协作/index.vue',s=>s.replace('<nav class="portal-tabs">','<nav class="portal-tabs">\n          <RouterLink v-if="router.hasRoute(\'任务栏\')" class="tab-link" :to="{ name: \'任务栏\' }">任务总览</RouterLink>'));
edit('src/views/推送首页/index.vue',s=>s.replace('      <RouterLink class="btn-line btn-primary" to="/write/blog">开始创作 ↗</RouterLink>','').replace(/    <nav class="home-shortcuts"[\s\S]*?<\/nav>\n/,'').replace(/const entries = \[[\s\S]*?\]\n/,'').replace(/^.*\.home-shortcuts[^\n]*\n/gm,'').replace('.home-welcome .btn-line { flex-shrink: 0; }\n',''));
// 首页不再重复全站菜单；保留公告、反馈、更新记录、签到与数据面板。
edit('src/views/推送首页/index.vue',s=>s.replace('</style>','@media(max-width: 1150px) { .content-layout { grid-template-columns: minmax(0,1fr) 280px; } }\n@media(max-width: 680px) { .home-welcome { padding-top: 8px; } .content-layout { grid-template-columns: minmax(0,1fr); } .main-content > *, .side-widgets > * { padding: 18px; } }\n</style>'));
