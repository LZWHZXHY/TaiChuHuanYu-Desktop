// Services/LingMai/LingMaiService.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.LingMai;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Utils;

namespace TaiChuWeb_V2.Services.LingMai
{
    public class LingMaiService
    {
        private readonly AppDbContext _context;

        public LingMaiService(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateSnapshotAsync(Guid noteId, string contentJson, string remark = "自动备份")
        {
            // 1. 开启事务，保证“存新”和“删旧”要么都成功，要么都失败
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 2. 插入当前最新的快照
                var newHistory = new NoteHistory
                {
                    NoteId = noteId,
                    ContentJson = contentJson,
                    Remark = remark,
                    CreatedAt = DateTime.UtcNow
                };
                _context.NoteHistories.Add(newHistory);
                await _context.SaveChangesAsync();

                // 3. 🌟 限制数量：获取该笔记所有的历史记录，按时间倒序排
                var historyList = await _context.NoteHistories
                    .Where(h => h.NoteId == noteId)
                    .OrderByDescending(h => h.CreatedAt)
                    .ToListAsync();

                // 4. 如果超过 20 份，删除多余的旧版本
                if (historyList.Count > 20)
                {
                    var oldVersions = historyList.Skip(20).ToList();
                    _context.NoteHistories.RemoveRange(oldVersions);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task RollbackToSnapshotAsync(Guid noteId, Guid historyId)
        {
            var history = await _context.NoteHistories
                .FirstOrDefaultAsync(h => h.NoteId == noteId && h.Id == historyId);

            if (history == null) throw new Exception("未找到该版本记录");

            // 🌟 提前拿到所属的 SpaceId 供双链使用
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
            if (note == null) throw new Exception("未找到该笔记");

            using var doc = JsonDocument.Parse(history.ContentJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("content", out var contentArray)) return;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. 🌟 修改点 1：使用多态指针 OwnerId + OwnerType 物理删除旧块
                var oldBlocks = _context.Blocks.Where(b => b.OwnerId == noteId.ToString() && b.OwnerType == "note");
                _context.Blocks.RemoveRange(oldBlocks);

                // 🌟 关键：先保存一次，确保旧 ID 彻底释放
                await _context.SaveChangesAsync();

                var insertedBlocks = new List<Block>();

                // 2. 插入新块
                foreach (var node in contentArray.EnumerateArray())
                {
                    // 🌟 修复点 1：安全获取 attrs
                    if (!node.TryGetProperty("attrs", out var attrs))
                    {
                        using var emptyDoc = JsonDocument.Parse("{}");
                        attrs = emptyDoc.RootElement.Clone();
                    }

                    // 🌟 修复点 2：安全获取 Type
                    string nodeType = node.TryGetProperty("type", out var typeProp)
                        ? typeProp.GetString() ?? "paragraph"
                        : "paragraph";

                    string blockId = attrs.TryGetProperty("id", out var idProp)
                        ? idProp.GetString()!
                        : Guid.NewGuid().ToString();

                    var attrsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(attrs.GetRawText()) ?? new();
                    attrsDict["id"] = blockId;

                    var newBlock = new Block
                    {
                        Id = blockId,
                        OwnerId = noteId.ToString(), // 🌟 修改点 2：改用 OwnerId
                        OwnerType = "note",          // 🌟 修改点 3：增加 OwnerType
                        Type = nodeType,
                        Data = JsonSerializer.Serialize(new
                        {
                            attrs = attrsDict,
                            content = node.TryGetProperty("content", out var c) ? c : (object?)null
                        }),
                        UpdatedAt = DateTime.UtcNow,
                        SortOrder = ""
                    };

                    _context.Blocks.Add(newBlock);
                    insertedBlocks.Add(newBlock);
                }

                await _context.SaveChangesAsync();

                // 🌟 3. 联动解析并固化双链
                await SyncNoteLinksAsync(noteId, note.SpaceId, insertedBlocks);

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                throw new Exception($"数据库回滚失败: {innerMsg}");
            }
        }

        public async Task SyncNoteBlocksAsync(NoteSyncDto dto)
        {
            // 🌟 1. 获取当前笔记详情，拿到 SpaceId 备用
            var note = await _context.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == dto.NoteId);
            if (note == null) return;

            // 🌟 修改点 1：使用 OwnerId 和 OwnerType 过滤出旧的块
            var existingIds = await _context.Blocks
                .Where(b => b.OwnerId == dto.NoteId.ToString() && b.OwnerType == "note")
                .Select(b => b.Id)
                .ToListAsync();

            var incomingIds = dto.Blocks.Select(b => b.Id).ToList();
            var currentBlocksInRequest = new List<Block>();

            // 2. 准备要处理的列表
            foreach (var blockDto in dto.Blocks)
            {
                if (existingIds.Contains(blockDto.Id))
                {
                    var block = new Block { Id = blockDto.Id };
                    _context.Blocks.Attach(block);

                    block.Data = blockDto.Data;
                    block.Type = blockDto.Type;
                    block.SortOrder = blockDto.SortOrder ?? "";
                    block.UpdatedAt = DateTime.UtcNow;

                    _context.Entry(block).Property(x => x.Data).IsModified = true;
                    _context.Entry(block).Property(x => x.SortOrder).IsModified = true;
                    _context.Entry(block).Property(x => x.UpdatedAt).IsModified = true;

                    currentBlocksInRequest.Add(block);
                }
                else
                {
                    var newBlock = new Block
                    {
                        Id = blockDto.Id,
                        OwnerId = dto.NoteId.ToString(), // 🌟 修改点 2：改用 OwnerId
                        OwnerType = "note",              // 🌟 修改点 3：增加 OwnerType 
                        Type = blockDto.Type,
                        Data = blockDto.Data,
                        SortOrder = blockDto.SortOrder ?? "",
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Blocks.Add(newBlock);
                    currentBlocksInRequest.Add(newBlock);
                }
            }

            // 3. 处理【物理删除】
            var idsToDelete = existingIds.Except(incomingIds).ToList();
            if (idsToDelete.Any())
            {
                var toDelete = idsToDelete.Select(id => new Block { Id = id });
                _context.Blocks.RemoveRange(toDelete);
            }

            try
            {
                await _context.SaveChangesAsync();

                // 🌟 4. 核心新增：同步保存块之后，执行双链解析并固化到 note_links 表
                await SyncNoteLinksAsync(dto.NoteId, note.SpaceId, currentBlocksInRequest);
            }
            catch (DbUpdateConcurrencyException)
            {
                Console.WriteLine("检测到并发冲突，已跳过本次过时更新。");
            }
        }

        /// <summary>
        /// 🌟 私有提取器：从最新的 Blocks 集合中提取双链并更新 note_links 表
        /// </summary>
        private async Task SyncNoteLinksAsync(Guid sourceNoteId, Guid spaceId, List<Block> currentBlocks)
        {
            var extractedTargetIds = new HashSet<Guid>();

            // 1. 递归扫描块中的 Data，提取 spiritLink
            foreach (var block in currentBlocks)
            {
                if (string.IsNullOrEmpty(block.Data)) continue;
                ExtractSpiritLinksFromJson(block.Data, extractedTargetIds);
            }

            // 2. 获取数据库中目前由这个笔记延伸出的旧链接
            var existingLinks = await _context.NoteLinks
                .Where(nl => nl.SourceNoteId == sourceNoteId)
                .ToListAsync();

            var existingTargetIds = existingLinks.Select(el => el.TargetNoteId).ToHashSet();

            // 3. 对比并找出需要“新增”和“删除”的链接
            var idsToAdd = extractedTargetIds.Where(id => !existingTargetIds.Contains(id)).ToList();
            var linksToRemove = existingLinks.Where(el => !extractedTargetIds.Contains(el.TargetNoteId)).ToList();

            // 4. 执行变更
            if (linksToRemove.Any())
            {
                _context.NoteLinks.RemoveRange(linksToRemove);
            }

            foreach (var targetId in idsToAdd)
            {
                if (targetId == sourceNoteId) continue; // 排除自己引用自己

                var newLink = new NoteLink
                {
                    Id = Guid.NewGuid(),
                    SpaceId = spaceId,
                    SourceNoteId = sourceNoteId,
                    TargetNoteId = targetId,
                    Excerpt = "上下文引用",
                    CreatedAt = DateTime.UtcNow
                };
                _context.NoteLinks.Add(newLink);
            }

            await _context.SaveChangesAsync();
        }

        private void ExtractSpiritLinksFromJson(string blockDataJson, HashSet<Guid> targetIds)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(blockDataJson))
                {
                    FindSpiritLinksInElement(doc.RootElement, targetIds);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[双链解析异常]: {ex.Message}");
            }
        }

        private void FindSpiritLinksInElement(JsonElement element, HashSet<Guid> targetIds)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                // 如果当前层级是一个 spiritLink 节点
                if (element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "spiritLink")
                {
                    if (element.TryGetProperty("attrs", out var attrsProp) &&
                        attrsProp.TryGetProperty("id", out var idProp))
                    {
                        if (Guid.TryParse(idProp.GetString(), out Guid linkedId))
                        {
                            targetIds.Add(linkedId);
                        }
                    }
                }

                // 递归向下遍历 content 属性数组
                if (element.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in contentProp.EnumerateArray())
                    {
                        FindSpiritLinksInElement(child, targetIds);
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray())
                {
                    FindSpiritLinksInElement(child, targetIds);
                }
            }
        }
    }
}