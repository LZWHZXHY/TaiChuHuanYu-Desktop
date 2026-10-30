from pathlib import Path
import re
backup=Path('.tmp/sharp-design/before')
def edit(name, changes):
 p=Path(name); original=p.read_text(encoding='utf-8-sig'); text=original
 for old,new in changes:
  if old not in text: raise ValueError(name+': missing '+old[:80])
  text=text.replace(old,new)
 if text!=original:
  dest=backup/p; dest.parent.mkdir(parents=True,exist_ok=True)
  if not dest.exists(): dest.write_bytes(p.read_bytes())
  p.write_text(text,encoding='utf-8')

# 复用已存在的页头装饰；不新增业务结构与入口。
for name,root,header,tone in [
 ('赞助墙/index.vue','sponsor-page content-page','sp-header','tone-profile'),
 ('版本更新/index.vue','release-page content-page','rel-header','tone-library'),
 ('开发进度/index.vue','report-page content-page','report-header','tone-collab'),
 ('开发进度/ReportDetail.vue','report-detail content-page','article-header','tone-collab'),
 ('荣誉墙/index.vue','honor-wall-root content-page','content-header','tone-profile'),
 ('交易行/index.vue','editorial-container content-page','editorial-header','tone-library'),
]:
 edit('src/views/'+name,[(f'class="{root}"',f'class="{root} community-surface {tone}"'),(f'<header class="{header}">',f'<header class="{header} surface-heading"><SectionTrace />'),('<script setup lang="ts">','<script setup lang="ts">\nimport SectionTrace from \'@/components/SectionTrace.vue\'')])

edit('src/views/世界观管理/index.vue',[
 ('<div class="world-cover">','<div class="world-cover" data-depth>'),
 ('.world-card { min-width: 0; background: var(--paper-card); border: 1px solid var(--border-line);','.world-card { min-width: 0; background: var(--paper-card); border: 0; border-bottom: 2px solid var(--border-line);'),
 ('.world-link { display: block; padding: 16px 16px 20px;','.world-link { display: block; padding: 0 0 20px;'),
 ('.world-link h2 { font-size:','.world-link h2 { padding-inline: 20px; font-size:'),
 ('.world-link p { color:','.world-link p { padding-inline: 20px; color:')])
edit('src/views/赞助墙/index.vue',[
 ('.sp-card { padding: 18px; border: 1px solid var(--border-line);','.sp-card { display: flex; flex-direction: column; padding: 24px; border: 0;'),
 ('background: #e8f6ee; color: #2da44e;','background: var(--success-soft); color: var(--success);'),
 ('background: #e8f1fb; color: #4a90d9;','background: var(--info-soft); color: var(--info);'),
 ('background: #f1eafb; color: #9b6dd6;','background: var(--accent-soft); color: var(--accent-color);'),
 ('background: #fbf3e0; color: #b8860b;','background: var(--warning-soft); color: var(--warning);'),
 ('border-top-color: #54b677;','border-top-color: var(--success);'),
 ('border-top-color: #4a90d9;','border-top-color: var(--info);'),
 ('border-top-color: #9b6dd6;','border-top-color: var(--accent-color);'),
 ('border-top-color: #d4a017;','border-top-color: var(--warning);'),
 ('.sp-stat { display: grid; gap: 6px; padding: 18px 20px; border: 1px solid var(--border-line);','.sp-stat { display: grid; gap: 6px; padding: 18px 20px; border: 0; border-left: 2px solid var(--signal-mint);'),
 ('background: rgba(0,0,0,.45);','background: var(--overlay);'),
 ('.sp-editor { width:', '.sp-editor { border-top: 2px solid var(--signal-mint); box-shadow: var(--shadow-md); width:')])
edit('src/views/版本更新/index.vue',[
 ('.rel-card { border: 1px solid var(--border-line);','.rel-card { border: 0; border-top: 1px solid var(--border-line);'),
 ('.rel-item.latest .rel-card { border-color: var(--accent-color); }','.rel-item.latest .rel-card { border-top: 2px solid var(--accent-color); background: linear-gradient(115deg,var(--accent-soft),var(--paper-card) 65%); }'),
 ('background: rgba(0,0,0,.45);','background: var(--overlay);'),
 ('.rel-editor { width:', '.rel-editor { border-top: 2px solid var(--signal-mint); box-shadow: var(--shadow-md); width:')])
edit('src/views/荣誉墙/index.vue',[
 ('padding: 24px; border: 1px solid var(--border-line);','padding: 24px; border: 0; border-left: 2px solid var(--signal-mint);'),
 ('padding: 16px 24px; border: 1px solid var(--border-line);','padding: 20px 24px; border: 0; border-bottom: 1px solid var(--border-line);'),
 ('.podium-card:hover { transform: translateY(-4px); }','.podium-card:hover { border-bottom-color: var(--signal-mint); }'),
 ('border-color: #d4af37; box-shadow: 0 12px 34px rgba(212, 175, 55, .20);','border-color: var(--accent-color); background: linear-gradient(145deg,var(--accent-soft),var(--paper-card) 75%);'),
 ('border-color: #aeb7c2;','border-color: var(--border-line);'),
 ('border-color: #b87333;','border-color: var(--accent-mint);')])

edit('src/views/管理面板/管理组件/邮件管理.vue',[(v,n) for v,n in [('#92400e','var(--warning)'),('#b45309','var(--warning)'),('#7e22ce','var(--accent-color)'),('#be123c','var(--danger)'),('#15803d','var(--success)')]])
edit('src/views/管理面板/管理组件/活动组件.vue',[('#e68a2e','var(--warning)'),('#4c9f70','var(--success)')])
edit('src/views/管理面板/管理组件/反馈组件.vue',[('#3b82f6','var(--accent-color)')])
edit('src/views/管理面板/管理组件/维基组件子类/RevisionReview.vue',[('#28a745','var(--success)'),('#007bff','var(--info)'),('#b7eb8f','var(--success)'),('#91d5ff','var(--info)')])
