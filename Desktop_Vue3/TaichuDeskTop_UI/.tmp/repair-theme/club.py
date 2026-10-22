from pathlib import Path
import re
p=Path('src/views/太初俱乐部/index.vue'); s=p.read_text(encoding='utf-8-sig')
a=s.index('      <!-- ==================== Hero'); b=s.index('      <!-- ==================== 数据状态',a); s=s[:a]+s[b:]
s=s.replace('<div class="page-shell">','<div class="page-shell club-home">',1)
s=s.replace('              </p>\n\n            </div>', '''              </p>
              <div class="club-actions"><RouterLink to="/taichu/operators" class="btn-line btn-primary">寻找陪玩</RouterLink><RouterLink to="/taichu/orders" class="btn-line">我的订单</RouterLink><a href="#club-register" class="btn-line" @click.prevent="handleJoin">加入俱乐部</a></div>
            </div>''',1)
s=s.replace('<section class="section-block">','<section id="club-register" ref="registerSection" class="section-block">')
s=s.replace("      console.log('点击立即加入')", "      this.$refs.registerSection?.scrollIntoView({ behavior: 'smooth', block: 'start' })")
s=s.replace("    handleLearnMore() {\n      console.log('点击了解我们')\n    },\n",'')
s=s.replace('OPERATIONS STATUS','俱乐部概况').replace('LIVE DATA','社区服务数据')
s=s[:s.index('<style scoped>')]+'''<style scoped>
.club-home { max-width: 1200px; margin: auto; }
.club-hero-section { padding-top: 24px; }
.club-grid-bg, .scan-line, .club-hero-bottom, .perk-card-line, .perk-heading-line, .operation-line, .operation-index { display: none; }
.club-intro { padding: 32px; border: 1px solid var(--line); border-radius: 16px; background: var(--panel); margin-bottom: 28px; }
.club-eyebrow { color: var(--orange); font-size: 12px; letter-spacing: .08em; }
.club-title { font-size: clamp(28px,3vw,40px); line-height: 1.3; margin: 12px 0; }
.club-lead, .club-lead-sub { color: var(--text-soft); font-size: 14px; line-height: 1.8; max-width: 52em; }
.club-actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 24px; }
.club-meta { display: flex; gap: 24px; flex-wrap: wrap; margin-top: 28px; padding-top: 20px; border-top: 1px solid var(--line); }
.club-meta-item span { display: block; font-size: 10px; color: var(--text-muted); margin-bottom: 4px; }
.club-meta-item strong { font-size: 13px; font-weight: 500; }
.perk-heading, .perk-top, .perk-foot-bar, .merch-head, .event-head, .operations-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
.perk-heading { font-size: 13px; margin-bottom: 16px; }
.perk-heading-label, .perk-index, .merch-eyebrow, .event-eyebrow, .merch-phase, .event-phase, .section-index { font-size: 11px; color: var(--text-muted); }
.perk-intro { line-height: 1.8; font-size: 14px; color: var(--text-soft); margin-bottom: 20px; }
.perk-note, .merch-note, .event-note, .perk-foot-note { display: block; color: var(--text-muted); font-size: 12px; margin-top: 8px; }
.perk-grid { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 20px; }
.perk-card, .merch-block, .event-item, .operation-item { border: 1px solid var(--line); border-radius: 12px; padding: 24px; background: var(--panel); }
.perk-tag, .event-status, .merch-status { display: inline-block; color: var(--orange); background: var(--accent-soft); font-size: 11px; padding: 4px 8px; border-radius: 6px; }
.perk-title { font-size: 24px; margin: 20px 0 12px; line-height: 1.5; }
.perk-title br { display: none; }
.perk-desc, .perk-list, .merch-desc, .merch-item p, .event-item p { color: var(--text-soft); font-size: 13px; line-height: 1.8; }
.perk-list { padding-left: 20px; margin-top: 14px; }
.perk-foot-bar { margin: 16px 0 32px; padding: 16px; border-radius: 10px; background: var(--bg-soft); font-size: 12px; }
.merch-block, .event-block { margin-bottom: 32px; }
.merch-head h3, .event-head h3 { font-size: 22px; margin: 6px 0; }
.merch-desc { margin: 16px 0 24px; }
.merch-list, .event-list { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: 20px; }
.merch-item { border-top: 1px solid var(--line); padding-top: 16px; }
.merch-item h4, .event-item h4 { margin: 8px 0; font-size: 15px; }
.event-list { margin-top: 20px; }
.event-status { margin-top: 16px; }
.operations-section { margin: 32px 0; }
.operations-heading { margin-bottom: 20px; }
.operations-heading > div { display: flex; gap: 12px; align-items: center; }
.operations-heading h2 { font-size: 22px; }
.live-status { font-size: 12px; color: var(--text-soft); }
.operations-grid { display: grid; grid-template-columns: repeat(4,minmax(0,1fr)); gap: 16px; }
.operation-number { font-size: 32px; font-weight: 650; }
.operation-detail { display: grid; gap: 6px; margin-top: 16px; color: var(--text-soft); font-size: 12px; }
.operation-label { font-size: 10px; }
@media(max-width: 700px) { .club-intro { padding: 24px; } .perk-grid, .merch-list, .event-list { grid-template-columns: minmax(0,1fr); } .operations-grid { grid-template-columns: repeat(2,minmax(0,1fr)); } }
</style>
'''
s=re.sub(r'\n\s*\n', '\n\n', s)
p.write_text(s,encoding='utf-8')
p=Path('src/views/太初俱乐部/Navbar.vue'); s=p.read_text(encoding='utf-8-sig').replace('to="/" class="logo-area"','to="/Club" class="logo-area"'); s=s[:s.index('<style scoped>')]+'''<style scoped>
.navbar { padding: 16px 0; border-bottom: 1px solid var(--line); background: var(--bg); }
.nav-container { display: flex; align-items: center; flex-wrap: wrap; gap: 16px; }
.logo-area { display: flex; text-decoration: none; color: var(--text); margin-right: auto; }
.logo-text { display: flex; gap: 8px; align-items: center; }
.logo-title { font-size: 18px; font-weight: 700; }
.logo-subtitle, .logo-divider { font-size: 12px; color: var(--text-soft); }
.logo-mark, .nav-index { display: none; }
.nav-links, .nav-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 6px; }
.nav-item, .btn-apply, .btn-cs { display: inline-flex; align-items: center; gap: 6px; padding: 9px 12px; border-radius: 8px; color: var(--text-soft); text-decoration: none; font-size: 13px; }
.nav-item:hover, .nav-item.active, .btn-apply, .btn-cs { background: var(--accent-soft); color: var(--orange); }
.nav-actions i { font-style: normal; }
@media(max-width: 1100px) { .nav-links { order: 3; width: 100%; } }
</style>
'''; p.write_text(s,encoding='utf-8')
