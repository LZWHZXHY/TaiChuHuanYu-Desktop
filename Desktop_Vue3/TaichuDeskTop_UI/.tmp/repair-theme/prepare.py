from pathlib import Path
import json
root=Path('E:/TaiChuHuanYu_DeskTop/TaiChuHuanYu-Desktop')
backend=root/'TaiChuBackend_V2/TaiChuWeb_V2/TaiChuWeb_V2'
stage=Path('.tmp/repair-theme/backend')
stage.mkdir(parents=True, exist_ok=True)
patches={
 'Controllers/ArtworkController.cs': [('ids.Contains(a.Id)', 'Enumerable.Contains(ids, a.Id)')],
 'Controllers/knowledgeBase/CatalogController.cs': [
 ('s.Id == n.OriginalNoteId', 's.Id.ToString() == EF.Functions.Collate(n.OriginalNoteId.ToString(), "utf8mb4_general_ci")'),
 ('artworkIds.Contains(t.EntityId)', 'Enumerable.Contains(artworkIds, t.EntityId)')],
 'Controllers/LingMai/LingMaiPublishController.cs': [
 ('n.Id == p.OriginalNoteId', 'n.Id.ToString() == EF.Functions.Collate(p.OriginalNoteId.ToString(), "utf8mb4_general_ci")')]
}
for relative,replacements in patches.items():
 source=(backend/relative).read_text(encoding='utf-8-sig')
 (stage/(Path(relative).name+'.before')).write_text(source,encoding='utf-8')
 for old,new in replacements:
  assert old in source, (relative, old)
  source=source.replace(old,new)
 (stage/Path(relative).name).write_text(source,encoding='utf-8')
(stage/'manifest.json').write_text(json.dumps(patches,ensure_ascii=False),encoding='utf-8')
