from pathlib import Path
p=Path('src/views/世界观管理/世界观组件/ProjectHeader.vue');s=p.read_text(encoding='utf-8');s=s.replace('导出 Markdown / PDF','导出 / 完整备份').replace('导入 TXT / Markdown','导入 / 恢复备份');p.write_text(s,encoding='utf-8')
