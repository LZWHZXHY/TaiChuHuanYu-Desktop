from pathlib import Path
import json
p=Path('.tmp/cleanup-batch17/baseline.json');d=json.loads(p.read_text(encoding='utf-8'));f='src/views/画廊展厅/ArtworkPublishModal.vue';d[f]=Path(f).read_text(encoding='utf-8-sig');p.write_text(json.dumps(d),encoding='utf-8')
p=Path(f);s=p.read_text(encoding='utf-8-sig').replace('.modal-body { margin: 0; border: 0; min-width: 0; }','.modal-body { margin: 0; border: 0; min-width: 0; min-height: 0; flex: 1; }')
s=s.replace('.modal-header {\n', '.modal-header {\n  flex-shrink: 0;\n  gap: 16px;\n').replace('.modal-footer {\n', '.modal-footer {\n  flex-shrink: 0;\n  background: var(--paper-bg);\n')
s=s.replace('.close-btn { background:', '.close-btn { flex-shrink: 0; align-self: flex-start; background:')
s=s.replace('.btn-cancel { background: var(--paper-card); border: none;', '.btn-cancel { color: var(--text-primary); background: var(--paper-card); border: 1px solid var(--border-line);')
s=s.replace('  .modal-body { grid-template-columns: 1fr; }', '  .modal-body { grid-template-columns: 1fr; padding: 20px; gap: 24px; }\n  .modal-header, .modal-footer { padding: 20px; }\n  .item-mask { opacity: 1; }')
p.write_text(s,encoding='utf-8')
p=Path('src/assets/styles/content.css');s=p.read_text(encoding='utf-8').replace('background: var(--text-primary); color: var(--paper-card); border-color: var(--text-primary);','background: var(--accent-color); color: #fff; border-color: var(--accent-color);')
p.write_text(s,encoding='utf-8')
