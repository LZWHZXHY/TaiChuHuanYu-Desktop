from pathlib import Path
import re
for p in Path('src/views/太初俱乐部').rglob('*.vue'):
 s=p.read_text(encoding='utf-8-sig')
 def fix(m):
  css=m[2]
  css=re.sub(r'rgba\((?:16,\s*25,\s*35|10,\s*15,\s*21|5,\s*8,\s*12|14,\s*23,\s*32),\s*[.\d]+\)', 'var(--panel)',css)
  css=css.replace('#a5a5ff','var(--accent-color)').replace('#b8b8ff','var(--accent-color)').replace('#ffb800','var(--warning)').replace('#ffd166','var(--warning)').replace('#4a9eff','var(--accent-color)')
  return m[1]+css+m[3]
 n=re.sub(r'(<style[^>]*>)([\s\S]*?)(</style>)',fix,s)
 if n!=s:p.write_text(n,encoding='utf-8')
# Replace obsolete oversized layouts, not append competing overrides.
p=Path('src/views/太初协作/协作组件/ProjectDetail.vue');s=p.read_text(encoding='utf-8-sig')
s=s.replace("computed, h",'computed').replace('返回灵脉大厅','返回协作大厅')
start=s.index('/**\n * 局部内联占位');end=s.index('const componentMap:',start);s=s[:start]+s[end:]
start=s.index('// 🌟 动态计算');end=s.index('const getStatusLabel',start);s=s[:start]+"const activeComponent = computed(() => componentMap[currentTab.value] ?? ProjectKanban);\n\n"+s[end:]
s=s[:s.index('<style scoped>')]+'''<style scoped>
.project-detail-wrapper { color: var(--text-primary); display: grid; gap: 24px; min-width: 0; }
.detail-header { display: flex; justify-content: space-between; gap: 24px; padding: 28px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-lg); }
.breadcrumb { display: inline-flex; gap: 8px; color: var(--accent-color); cursor: pointer; margin-bottom: 16px; }
.project-name { font-size: clamp(24px, 3vw, 34px); margin: 0 0 12px; overflow-wrap: anywhere; }
.project-description { max-width: 700px; color: var(--text-secondary); line-height: 1.7; white-space: pre-wrap; }
.meta-card { display: grid; gap: 16px; min-width: 170px; }
.meta-row { display: grid; gap: 6px; font-size: 14px; }
.label { color: var(--text-secondary); }
.status-badge { color: var(--success); }
.module-nav { min-width: 0; border-bottom: 1px solid var(--border-line); }
.nav-container { display: flex; gap: 8px; overflow-x: auto; padding-bottom: 10px; }
.nav-link { flex-shrink: 0; padding: 10px 16px; border: 0; border-radius: var(--radius-sm); background: transparent; color: var(--text-secondary); cursor: pointer; }
.nav-link:hover { background: var(--bg-sub); }
.nav-link.is-active { background: var(--accent-soft); color: var(--accent-color); font-weight: 600; }
.content-viewport, .module-container { min-width: 0; }
.loading-state { padding: 60px 20px; text-align: center; color: var(--text-secondary); }
.loading-pulse { width: 40px; height: 4px; margin: auto; background: var(--accent-color); }
.view-fade-enter-active, .view-fade-leave-active { transition: opacity .15s; }
.view-fade-enter-from, .view-fade-leave-to { opacity: 0; }
@media (max-width: 700px) { .detail-header { flex-direction: column; padding: 20px; } .meta-card { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
</style>
''';s=s.replace('<nav class="breadcrumb" @click="router.push(\'/Project\')">','<router-link class="breadcrumb" to="/Project">').replace('</nav>\n        <div class="title-group">','</router-link>\n        <div class="title-group">');p.write_text(s,encoding='utf-8')
p=Path('src/views/管理面板/index.vue');s=p.read_text(encoding='utf-8-sig')
s=s.replace('<div \n          v-for="menu', '<button type="button"\n          v-for="menu').replace('<div class="active-bar"></div>\n        </div>', '</button>').replace('<span class="logo-text">太初</span>','<span class="logo-text">管理中心</span>').replace('<span class="version">V2.0</span>','')
s=re.sub(r'        <div class="global-stats">[\s\S]*?\n        </div>\n      </header>', '      </header>',s)
s=s.replace('<span class="role">系统审计师</span>','<span class="role">内容与社区治理</span>')
s=s[:s.index('<style scoped>')]+'''<style scoped>
.admin-layout { display: grid; grid-template-columns: 168px minmax(0, 1fr); gap: 24px; color: var(--text-primary); align-items: start; }
.admin-sidebar { position: sticky; top: 0; padding: 16px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-md); }
.sidebar-brand { font-size: 18px; font-weight: 700; padding: 8px 10px 20px; }
.nav-group { display: grid; gap: 4px; }
.nav-item { display: flex; align-items: center; gap: 10px; padding: 11px 12px; width: 100%; background: transparent; color: var(--text-secondary); border: none; border-radius: var(--radius-sm); text-align: left; cursor: pointer; }
.nav-item:hover { background: var(--bg-sub); }
.nav-item.is-active { background: var(--accent-soft); color: var(--accent-color); font-weight: 600; }
.sidebar-footer { margin-top: 20px; padding: 14px 10px 0; border-top: 1px solid var(--border-line); }
.admin-info { display: grid; gap: 6px; font-size: 12px; color: var(--text-secondary); }
.admin-main, .content-body { min-width: 0; }
.main-header { padding: 12px 0 20px; border-bottom: 1px solid var(--border-line); margin-bottom: 24px; }
.breadcrumb { display: flex; gap: 12px; font-size: 14px; }
.breadcrumb .root, .separator { color: var(--text-secondary); }
.content-body :deep(table) { width: 100%; }
.content-body :deep(.table-container), .content-body :deep(.table-wrapper) { overflow-x: auto; }
.fade-transform-enter-active, .fade-transform-leave-active { transition: opacity .15s; }
.fade-transform-enter-from, .fade-transform-leave-to { opacity: 0; }
@media (max-width: 1100px) { .admin-layout { grid-template-columns: minmax(0, 1fr); gap: 16px; } .admin-sidebar { position: static; padding: 10px; } .sidebar-brand, .sidebar-footer { display: none; } .nav-group { display: flex; overflow-x: auto; } .nav-item { flex-shrink: 0; width: auto; white-space: nowrap; } }
</style>
''';p.write_text(s,encoding='utf-8')
