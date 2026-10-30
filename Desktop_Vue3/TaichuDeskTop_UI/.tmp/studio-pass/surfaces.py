from pathlib import Path
import re, shutil
r=Path.cwd(); backup=r/'.tmp/studio-pass/before'
def edit(path, fn):
 p=r/path; b=backup/path; b.parent.mkdir(parents=True,exist_ok=True)
 if not b.exists(): shutil.copy2(p,b)
 p.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')
def surface(path, root, heading=None, tone='tone-library', motion=True):
 def transform(s):
  s=s.replace('class="'+root+'"','class="'+root+' workbench-skin editor-surface community-surface '+tone+'"',1)
  if heading:
   s=s.replace('class="'+heading+'"','class="'+heading+' studio-heading"',1)
   if motion:
    s=re.sub(r'(<(?:header|div)[^>]*class="[^\"]*studio-heading"[^>]*>)',r'\1<SectionTrace />',s,count=1)
    s=re.sub(r'(<script setup[^>]*>)',r"\1\nimport SectionTrace from '@/components/SectionTrace.vue'",s,count=1)
  return s
 edit(path,transform)
surface('src/views/太初随笔/ComposeView.vue','compose-page content-page workbench-skin','content-header')
surface('src/views/世界观管理/ProjectCreate.vue','project-create workbench-skin','page-header','tone-world')
surface('src/views/世界观管理/世界观组件/CardEditor.vue','card-editor-inline workbench-skin',tone='tone-world')
surface('src/views/世界观管理/世界观组件/WorldTransfer.vue','transfer-dialog workbench-skin',tone='tone-world')
edit('src/views/世界观管理/世界观组件/WorldTransfer.vue',lambda s:s.replace('<header><h2>','<header class="studio-heading"><h2>',1))
surface('src/views/画廊展厅/ArtworkPublishModal.vue','publish-modal content-form','modal-header','tone-gallery')
surface('src/views/太初灵脉/components/PublishModal.vue','md-container','md-header')
surface('src/views/太初灵脉/components/QuickEditorDrawer.vue','quick-editor-drawer','quick-drawer-header')
surface('src/views/太初灵脉/components/NoteSettingsPanel.vue','note-settings-drawer workbench-skin','drawer-header',motion=False)
surface('src/views/太初灵脉/components/HistoryPanel.vue','spirit-history-panel workbench-skin','panel-header',motion=False)
surface('src/views/太初灵脉/components/WikiImportModal.vue','spirit-modal-content pop-enter-active')
edit('src/views/太初灵脉/components/TopBar.vue',lambda s:s.replace('class="editor-header"','class="editor-header studio-heading"',1))
edit('src/views/太初灵脉/SpiritLayout.vue',lambda s:s.replace('spirit-link-app workbench-skin','spirit-link-app workbench-skin editor-surface community-surface tone-library',1))
for path,root in [('OCs/components/create.vue','community-detail create-page'),('OCs/components/edit.vue','community-detail edit-page'),('Joint/components/JointCreate.vue','community-detail create-joint'),('Joint/components/JointEdit.vue','community-detail edit-joint'),('Battle/components/CreateBattle.vue','community-detail create-page')]:
 surface('src/views/柴圈板块/'+path,root,'page-header','tone-chai')
surface('src/views/柴圈板块/Works/Create.vue','create-page content-page','content-header','tone-chai')
surface('src/views/活动中心/问卷中心/SurveyEditor.vue','survey-editor-container','form-header')
surface('src/views/活动中心/打卡中心/CreateActivity.vue','form-card')
edit('src/views/活动中心/打卡中心/CreateActivity.vue',lambda s:s.replace('<h2><i class="fas fa-rocket"></i> 开启新挑战</h2>','<header class="studio-heading"><h2>开启新挑战</h2></header>',1))
for file,root,heading in [('ProjectTaskDetail.vue','task-detail-drawer','drawer-header'),('ProjectReports.vue','modal-window','modal-head'),('ProjectDeliveries.vue','minimal-modal','modal-inner-header'),('ProjectMember.vue','minimal-modal','modal-inner-header')]:
 surface('src/views/太初协作/协作组件/'+file,root,heading,'tone-collab',motion=file=='ProjectTaskDetail.vue')
def admin(s):
 s=s.replace("'workbench-skin': activeTab !== 'club'", "'workbench-skin editor-surface community-surface tone-admin': activeTab !== 'club'")
 s=s.replace('<header class="admin-heading">','<header class="admin-heading" :class="{ \'studio-heading\': activeTab !== \'club\' }"><SectionTrace v-if="activeTab !== \'club\'" />')
 return s.replace('<script setup lang="ts">',"<script setup lang=\"ts\">\nimport SectionTrace from '@/components/SectionTrace.vue'")
edit('src/views/管理面板/index.vue',admin)
for f,root,heading in [('公告组件.vue','md-modal-card workbench-skin','modal-header'),('用户组件.vue','modal-container scroll-y workbench-skin','modal-header'),('交易行组件.vue','modal-container mobile-full workbench-skin','modal-header'),('活动组件.vue','modal-content form-modal workbench-skin','modal-title'),('维基组件子类/RevisionReview.vue','modal-container workbench-skin','modal-header'),('维基组件子类/CategoryManager.vue','modal-container workbench-skin',None),('作品管理组件/画廊治理.vue','modal-container scroll-y workbench-skin','modal-header')]:
 surface('src/views/管理面板/管理组件/'+f,root,heading,'tone-admin',motion=False)
def doc(s):
 s=s.replace('      <!-- 标题 -->','      <div class="doc-lead" :class="{ \'has-cover\': card.coverImage }">\n      <!-- 标题 -->',1)
 s=s.replace('      <!-- 描述 -->','      </div>\n      <!-- 描述 -->',1)
 s=s.replace('class="doc-header"','class="doc-header studio-heading"',1)
 s=s.replace('max-width: 720px;', 'max-width: 820px;',1)
 s=s.replace('.doc-header {\n  margin-bottom: 32px;\n}', '.doc-lead { margin-bottom: 32px; border: 1px solid var(--surface-edge, var(--border-line)); border-top: 3px solid var(--surface-color, var(--accent-color)); background: linear-gradient(120deg,var(--paper-card),var(--surface-wash,var(--bg-sub))); }\n.doc-lead.has-cover { display: grid; grid-template-columns: minmax(0,1fr) 200px; }\n.doc-header { padding: 28px; align-self: center; }')
 s=s.replace('  margin-bottom: 32px;\n  border-radius: 8px;', '  margin: 16px;\n  border-radius: var(--radius-sm);',1)
 s=s.replace('  max-height: 320px;', '  max-height: 240px;',1)
 s=s.replace('  border-bottom: 1px solid var(--border-line);', '  border-bottom: 1px solid var(--surface-edge, var(--border-line));\n  border-left: 3px solid var(--surface-color, var(--accent-color));\n  padding-left: 12px;',1)
 s=s.replace('</style>','@media(max-width:640px) { .doc-lead.has-cover { grid-template-columns: minmax(0,1fr); }.doc-header { padding: 22px 18px; }.doc-lead .doc-cover { margin-top: 0; }.doc-meta { flex-wrap: wrap; } }\n</style>')
 return s
edit('src/views/世界观管理/世界观组件/ProjectCardDocument.vue',doc)
def embedded(s):
 s=s.replace(':class="{ \'is-clickable\': collapsible || clickable }"', ':class="{ \'is-clickable\': collapsible || clickable }"\n      :role="collapsible || clickable ? \'button\' : undefined"\n      :tabindex="collapsible || clickable ? 0 : undefined"\n      :aria-expanded="collapsible ? expanded : undefined"\n      @keydown.enter.self.prevent="onHeaderClick"\n      @keydown.space.self.prevent="onHeaderClick"',1)
 s=s.replace('  border-radius: 10px;', '  border-radius: var(--radius-sm);\n  border-left: 3px solid var(--surface-color, var(--accent-color));',1)
 s=s.replace('  background: var(--paper-card);\n  user-select:', '  background: linear-gradient(115deg,var(--surface-wash,var(--bg-sub)),var(--paper-card));\n  user-select:',1)
 s=s.replace('.ec-header.is-clickable:hover {\n  background: var(--paper-card);\n}', '.ec-header.is-clickable:is(:hover,:focus-visible) { background: var(--accent-soft); outline: 2px solid var(--accent-color); outline-offset: -2px; }')
 return s
edit('src/views/世界观管理/世界观组件/EmbeddedCard.vue',embedded)
