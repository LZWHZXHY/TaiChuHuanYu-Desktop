from pathlib import Path
for f in ['src/views/太初随笔/PublicNoteDetail.vue','src/views/画廊展厅/ArtworkDetail.vue']:
 p=Path(f);s=p.read_text(encoding='utf-8').replace('.md-controls {\n', '.md-controls {\n  position: sticky; top: 0; z-index: 10; background: var(--paper-bg);\n');p.write_text(s,encoding='utf-8')
p=Path('src/assets/styles/content.css');s=p.read_text(encoding='utf-8').replace('  align-items: flex-start;','  align-items: center;',1).replace('  padding: 12px 0 28px;','  padding: 28px 32px;',1).replace('  border-bottom: 1px solid var(--border-line);','  border: 1px solid var(--border-line);\n  border-radius: 16px;\n  background: var(--paper-sub);',1)
s=s.replace('.content-page .content-header { background: var(--paper-sub); padding: 28px 32px; border: 1px solid var(--border-line); border-radius: 16px; align-items: center; }\n','').replace('.content-page .content-header { gap: 16px; padding-top: 4px; margin-bottom: 16px; }','.content-page .content-header { margin-bottom: 16px; }')
p.write_text(s,encoding='utf-8')
p=Path('src/views/知识库/index.vue');s=p.read_text(encoding='utf-8').replace('.primary, .type-nav .active {','.type-nav .active {');p.write_text(s,encoding='utf-8')
p=Path('src/views/灵脉百科/components/WikiDetail.vue');s=p.read_text(encoding='utf-8').replace('.primary {\n  background: var(--text-primary);\n  color: var(--paper-bg);\n}\n','');p.write_text(s,encoding='utf-8')
