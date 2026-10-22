from pathlib import Path
import re
p=Path('src/views/柴圈板块/index.vue'); s=p.read_text(encoding='utf-8-sig')
s=s.replace('<div class="home" :class="{ \'theme-aged\': isAged }">','<div class="home content-page">')
a=s.index('    <!-- 顶部导航 -->'); b=s.index('    <!-- 主内容 -->'); s=s[:a]+'''    <header class="content-header"><div><p class="eyebrow">创作与同好</p><h1>柴圈社区</h1><p class="intro">为角色留下故事，找到一起创作的伙伴。</p></div><div class="content-actions"><RouterLink to="/ocs/create" class="primary">创建角色</RouterLink><RouterLink to="/joint/create">发起联合</RouterLink></div></header>
    <nav class="community-nav" aria-label="柴圈导航"><RouterLink to="/community" class="selected">社区总览</RouterLink><RouterLink to="/ocs">OC 角色</RouterLink><RouterLink to="/joint">联合创作</RouterLink><RouterLink to="/battles">约战广场</RouterLink></nav>

'''+s[b:]
s=s.replace('const isAged = ref(false)','')
a=s.index('// ===== 纸张切换 ====='); b=s.index('// ===== ⭐ 加载数据 ====='); s=s[:a]+s[b:]
s=s.replace("import { ref, computed, onMounted }", "import { computed, onMounted }")
s=re.sub(r'  --[\w-]+:.*?;\n', '', s)
s=re.sub(r'/\* 夜墨模式 \*/[\s\S]*?\n}\n', '', s)
a=s.index('/* ====== 顶部导航 ====== */'); b=s.index('/* ====== 通用组件 ====== */'); s=s[:a]+s[b:]
s=s.replace('min-height: 100vh;','min-height: 0;').replace('grid-template-columns: 2fr 1fr;','grid-template-columns: minmax(0, 1fr) 260px;').replace('  padding: 40px;','  padding: 0;').replace('  gap: 40px;','  gap: 24px;')
s=s.replace('<li>墨划系统 UI 设计规范 1.0 发布</li>\n            <li>关于柴圈约战系统判分规则调整</li>\n            <li>喜讯：第三届柴圈联合大作获奖公布</li>\n            <li>新手作画教程：如何保持线条柔顺</li>','<li><RouterLink to="/ocs/my">管理我的角色</RouterLink></li>\n            <li><RouterLink to="/joint/my">我的联合创作</RouterLink></li>\n            <li><RouterLink to="/battles/my">查看我的约战</RouterLink></li>')
s=s.replace('集所告示','我的创作').replace('在线柴友：1,280 人','进行中的联合：{{ jointTotal || 0 }} 场').replace('累计约战：','我的约战：').replace('TCV · 太初论战','TCV · 我的约战').replace('墨划 · 纸张极简设计 — 柴圈社区','柴圈社区 · 角色与共同创作')
s=s.replace('</style>', '''.notice-board, .battle-item, .joint-home-item, .oc-card { border-radius: 12px; background: var(--paper-card); }
.notice-list a { color: var(--text-primary); text-decoration: none; }
.notice-list a:hover { color: var(--accent-color); }
.home .navbar { padding: 0; }
@media(max-width: 1100px) { .container { grid-template-columns: minmax(0,1fr); } .sidebar { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 16px; } }
@media(max-width: 600px) { .sidebar { grid-template-columns: minmax(0,1fr); } .section-header-right { flex-wrap: wrap; } }
</style>''')
p.write_text(s,encoding='utf-8')
# 柴圈各表单、卡片、详情继承主题，保留状态色与画作原色。
mapping={'#f4f1ea':'var(--bg-main)','#f7f4ee':'var(--bg-main)','#fcfaf7':'var(--paper-card)','#fff':'var(--paper-card)','#ffffff':'var(--paper-card)','#ece8e0':'var(--bg-sub)','#f0ebe1':'var(--bg-sub)','#f0ebdf':'var(--bg-sub)','#f5f5f5':'var(--bg-sub)', '#2c2a29':'var(--text-primary)','#2a2826':'var(--text-primary)','#333':'var(--text-primary)','#1a1a1a':'var(--text-primary)','#7a7571':'var(--text-secondary)','#7a7570':'var(--text-secondary)','#999':'var(--text-secondary)','#666':'var(--text-secondary)','#d8d2c7':'var(--border-line)','#d5cec5':'var(--border-line)','#e0e0e0':'var(--border-line)','#ddd':'var(--border-line)','#ccc':'var(--border-line)','#9e2a2b':'var(--accent-color)'}
for p in Path('src/views/柴圈板块').rglob('*.vue'):
 s=p.read_text(encoding='utf-8-sig')
 s=re.sub(r'<style\b[\s\S]*?</style>', lambda m:re.sub(r'#[0-9a-fA-F]{3,8}\b', lambda x:mapping.get(x[0].lower(),x[0]),m[0]),s)
 p.write_text(s,encoding='utf-8')
p=Path('src/views/太虚游/index.vue'); s=p.read_text(encoding='utf-8-sig')
s=s.replace('<div class="game-hub" :class="{ \'theme-aged\': isAged }">','<div class="game-hub content-page">')
a=s.index('    <!-- 顶部品牌栏 -->'); b=s.index('    <div class="hub-body">'); s=s[:a]+'''    <header class="content-header"><div><p class="eyebrow">社区游乐场</p><h1>太虚游</h1><p class="intro">休息一下，和朋友开始一局游戏。</p></div><span class="connection-status" :class="isConnected ? 'online' : 'offline'">{{ isConnected ? '● 联机服务已连接' : '○ 联机服务未连接' }}</span></header>
'''+s[b:]
s=s.replace('<div\n              v-for="item in group.items"','<button type="button"\n              v-for="item in group.items"').replace('<span v-if="item.isNew" class="badge-new">新</span>\n            </div>','<span v-if="item.isNew" class="badge-new">新</span>\n            </button>')
s=re.sub(r'          <div class="stat-item">\s*<span class="stat-value">{{ onlineCount }}</span>[\s\S]*?</div>','',s)
a=s.index('          <div class="welcome-icon">'); b=s.index('        </div>',a); s=s[:a]+'''          <h2>选择你的下一场冒险</h2><p>按兴趣挑选游戏；联机功能需要连接服务器。</p>
          <div class="game-cards"><button v-for="(game, key) in gameModules" :key="key" class="game-card" @click="loadGame(key)"><span class="game-card-icon" aria-hidden="true">{{ game.icon }}</span><strong>{{ game.label }}</strong><small>{{ game.description }}</small><span class="game-status">{{ game.status }}</span></button></div>
'''+s[b:]
a=s.index('// ===== 纸色切换 ====='); b=s.index('// ===== SignalR 连接',a); s=s[:a]+s[b:]
s=s.replace("const hubUrl = 'https://bianyuzhou.com/signalr/game'", "const hubUrl = new URL('/signalr/game', new URL(import.meta.env.VITE_API_BASE_URL || '/api', window.location.origin)).href")
s=s.replace('const onlineCount = ref(1280)','').replace('  togglePaper()\n','')
s=re.sub(r'  --[\w-]+:.*?;\n','',s).replace('min-height: 100vh;','min-height: 0;')
s=s.replace('太虚游 · 墨划风格设计','太虚游 · 社区游乐场')
s=s.replace('</style>', '''.hub-body { gap: 24px; overflow: visible; align-items: start; }
.hub-sidebar { width: 190px; border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); }
.nav-item { width: 100%; text-align: left; border: 0; background: transparent; border-radius: 8px; font: inherit; }
.nav-item.active { background: var(--accent-soft); color: var(--accent-color); }
.hub-main { min-width: 0; padding: 0; overflow: visible; }
.welcome-placeholder { min-height: 0; display: block; padding: 0; text-align: left; }
.welcome-placeholder h2 { font-size: 22px; margin: 0 0 10px; }
.game-cards { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 16px; margin-top: 24px; }
.game-card { display: flex; flex-direction: column; align-items: flex-start; text-align: left; gap: 10px; padding: 24px; border: 1px solid var(--border-line); border-radius: 12px; color: var(--text-primary); background: var(--paper-card); }
.game-card:hover { border-color: var(--accent-color); }
.game-card-icon { font-size: 28px; }
.game-card small { color: var(--text-secondary); line-height: 1.7; flex: 1; }
.game-status { color: var(--accent-color); background: var(--accent-soft); border-radius: 6px; padding: 3px 8px; font-size: 12px; }
@media(max-width: 1100px) { .hub-body { flex-direction: column; } .hub-sidebar { width: 100%; padding: 12px; } .game-nav { display: flex; flex-wrap: wrap; gap: 8px; } .nav-group { margin: 0; } .nav-group-title, .sidebar-stats { display: none; } .hub-main { width: 100%; } }
@media(max-width: 560px) { .game-cards { grid-template-columns: minmax(0,1fr); } }
</style>''')
p.write_text(s,encoding='utf-8')
