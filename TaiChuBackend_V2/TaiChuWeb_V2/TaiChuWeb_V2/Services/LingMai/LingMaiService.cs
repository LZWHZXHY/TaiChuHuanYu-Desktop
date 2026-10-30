// Services/LingMai/LingMaiService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using TaiChuWeb_V2.Models.FinalNodes;
using TaiChuWeb_V2.Models.Tag;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.LingMai;
using TaiChuWeb_V2.Models.LingMai;

namespace TaiChuWeb_V2.Services.LingMai
{
    public class LingMaiService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LingMaiService>? _logger;

        // logger 可选：依赖注入会自动提供；回归测试直接 new LingMaiService(db) 仍可用。
        public LingMaiService(AppDbContext context, ILogger<LingMaiService>? logger = null)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<NoteReferenceDto>> GetNoteLinksAsync(Guid noteId, bool incoming)
        {
            var links = incoming
                ? _context.NoteLinks.Where(l => l.TargetNoteId == noteId)
                    .Select(l => new { id = l.SourceNoteId, note = l.SourceNote, l.Excerpt })
                : _context.NoteLinks.Where(l => l.SourceNoteId == noteId)
                    .Select(l => new { id = l.TargetNoteId, note = l.TargetNote, l.Excerpt });

            return await links.Select(l => new NoteReferenceDto
            {
                Id = l.id,
                Title = l.note != null ? l.note.Title : "无标题碎片",
                Excerpt = l.Excerpt,
                UpdatedAt = l.note != null ? l.note.UpdatedAt : DateTime.UtcNow
            }).ToListAsync();
        }

        public async Task CreateSnapshotAsync(Guid noteId, string contentJson, string remark = "自动备份")
        {
            // 1. 获取当前配置的弹性重试策略
            var strategy = _context.Database.CreateExecutionStrategy();

            // 2. 将整个事务放入重试策略中执行
            await strategy.ExecuteAsync(async () =>
            {
                // 3. 必须在策略内部显式开启事务
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 4. 插入当前最新的快照
                    var newHistory = new NoteHistory
                    {
                        Id = Guid.NewGuid(), // 建议显式生成赋值
                        NoteId = noteId,
                        ContentJson = contentJson,
                        Remark = remark,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.NoteHistories.Add(newHistory);
                    await _context.SaveChangesAsync();

                    // 5. 限制数量：获取该笔记所有的历史记录，按时间倒序排
                    var historyList = await _context.NoteHistories
                        .Where(h => h.NoteId == noteId)
                        .OrderByDescending(h => h.CreatedAt)
                        .ToListAsync();

                    // 6. 如果超过 20 份，删除多余的旧版本
                    if (historyList.Count > 20)
                    {
                        var oldVersions = historyList.Skip(20).ToList();
                        _context.NoteHistories.RemoveRange(oldVersions);
                        await _context.SaveChangesAsync();
                    }

                    // 7. 成功后提交事务
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    // 8. 发生异常时回滚
                    await transaction.RollbackAsync();

                    // 🛑 核心注意点：必须原样 throw 抛出异常！
                    // 只有抛出来，重试策略捕获后才知道发生了网络波动，进而触发下一次安全重试
                    throw;
                }
            });
        }

        // 兼容旧 Tiptap 快照；新快照携带原始 blocks，保留白板、日程及系统块的数据。
        public static NoteSyncDto ReadSnapshot(Guid noteId, string contentJson)
        {
            if (string.IsNullOrWhiteSpace(contentJson)) throw new JsonException();
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
            List<BlockSyncDto> blocks;
            if (root.TryGetProperty("blocks", out var storedBlocks))
            {
                var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };
                blocks = storedBlocks.Deserialize<List<BlockSyncDto>>(options) ?? throw new JsonException();
            }
            else
            {
                if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    throw new JsonException();
                blocks = content.EnumerateArray().Select((node, index) =>
                {
                    if (node.ValueKind != JsonValueKind.Object) throw new JsonException();
                    var id = node.TryGetProperty("attrs", out var attrs) && attrs.ValueKind == JsonValueKind.Object &&
                        attrs.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                    return new BlockSyncDto
                    {
                        Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id,
                        Type = node.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String ? type.GetString()! : "paragraph",
                        Data = node.GetRawText(), SortOrder = index
                    };
                }).ToList();
            }
            if (blocks.Any(b => b == null || string.IsNullOrEmpty(b.Id) || string.IsNullOrEmpty(b.Type) || string.IsNullOrEmpty(b.Data)))
                throw new JsonException();
            foreach (var block in blocks) { using var data = JsonDocument.Parse(block.Data); }
            return new NoteSyncDto { NoteId = noteId, Blocks = blocks };
        }

        // Conflict：多次重试后仍冲突，必须告诉前端「没保存成功」，不能再当作成功返回。
        public enum NoteSaveResult { Saved, NotFound, Forbidden, DeadlockIgnored, DuplicateIgnored, ConcurrencyIgnored, Conflict }

        private const int MaxSaveAttempts = 3;

        // 持久化及重试归服务处理；控制器负责将结果映射为 HTTP 响应。
        // 死锁(1213)、主键/唯一索引冲突(1062)、并发冲突通常来自同一篇笔记的两次保存交错：
        // 清空跟踪状态、稍等后整篇重写一次即可成功。重试仍失败则返回 Conflict，由前端提示并重试，绝不静默丢弃。
        public async Task<NoteSaveResult> SaveNoteAsync(NoteSyncDto dto, string userId)
        {
            for (var attempt = 1; ; attempt++)
            {
                var result = await SaveNoteOnceAsync(dto, userId);
                if (result is not (NoteSaveResult.DeadlockIgnored or NoteSaveResult.DuplicateIgnored or NoteSaveResult.ConcurrencyIgnored))
                    return result;
                _logger?.LogWarning("笔记 {NoteId} 第 {Attempt} 次保存冲突：{Result}", dto.NoteId, attempt, result);
                if (attempt >= MaxSaveAttempts) return NoteSaveResult.Conflict;
                _context.ChangeTracker.Clear();
                await Task.Delay(100 * attempt);
            }
        }

        private async Task<NoteSaveResult> SaveNoteOnceAsync(NoteSyncDto dto, string userId)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                var note = await _context.Notes.FindAsync(dto.NoteId);
                if (note == null) return NoteSaveResult.NotFound;

                var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == userId);
                if (!isOwner) return NoteSaveResult.Forbidden;

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 1. 更新基本元数据
                    if (!string.IsNullOrEmpty(dto.Title)) note.Title = dto.Title;
                    if (dto.ExtraData != null) note.ExtraData = dto.ExtraData;
                    note.UpdatedAt = DateTime.UtcNow;

                    // 准备收集所有的双链 ID
                    var currentOutlinkIds = new HashSet<Guid>();

                    var noteId = dto.NoteId.ToString();
                    bool isCanvasOrMap = note.Type == NoteTypes.Canvas || note.Type == NoteTypes.Map;
                    bool isSchedule = note.Type == NoteTypes.Schedule;
                    if (isCanvasOrMap)
                    {
                        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                        note.BlocksData = dto.Blocks?.Any() == true ? JsonSerializer.Serialize(dto.Blocks, options) : "[]";
                        CollectNoteReferences(note.BlocksData, dto.NoteId, currentOutlinkIds);
                        var obsoleteBlocks = await _context.Blocks.Where(b => b.OwnerId == noteId).ToListAsync();
                        if (obsoleteBlocks.Any()) _context.Blocks.RemoveRange(obsoleteBlocks);
                    }
                    else
                    {
                        await _context.Blocks.Where(b => b.OwnerId == noteId).ExecuteDeleteAsync();
                        var seenIds = new HashSet<string>();
                        int blockOrder = 0;
                        foreach (var b in dto.Blocks ?? new List<BlockSyncDto>())
                        {
                            // 日程沿用逐条写入；普通笔记仍跳过空 ID 和重复 ID。
                            if (!isSchedule && (string.IsNullOrEmpty(b.Id) || !seenIds.Add(b.Id))) continue;
                            _context.Blocks.Add(new Block
                            {
                                Id = b.Id, OwnerId = noteId, OwnerType = note.Type,
                                Type = b.Type, Data = b.Data ?? string.Empty,
                                SortOrder = b.SortOrder ?? blockOrder, UpdatedAt = DateTime.UtcNow
                            });
                            blockOrder++;
                            CollectNoteReferences(b.Data, dto.NoteId, currentOutlinkIds);
                        }
                    }

                    var existingLinks = await _context.NoteLinks
                        .Where(nl => nl.SourceNoteId == note.Id)
                        .ToListAsync();

                    var existingTargetIds = existingLinks.Select(nl => nl.TargetNoteId).ToHashSet();
                    var linksToRemove = existingLinks.Where(nl => !currentOutlinkIds.Contains(nl.TargetNoteId)).ToList();
                    if (linksToRemove.Any()) _context.NoteLinks.RemoveRange(linksToRemove);

                    var targetIdsToAdd = currentOutlinkIds.Where(id => !existingTargetIds.Contains(id)).ToList();
                    if (targetIdsToAdd.Any())
                    {
                        var validTargetIds = await _context.Notes.Where(n => targetIdsToAdd.Contains(n.Id)).Select(n => n.Id).ToListAsync();
                        foreach (var targetId in validTargetIds)
                        {
                            _context.NoteLinks.Add(new NoteLink
                            {
                                Id = Guid.NewGuid(),
                                SourceNoteId = note.Id,
                                TargetNoteId = targetId,
                                Excerpt = dto.Title ?? note.Title
                            });
                        }
                    }

                    if (dto.Tags != null)
                    {
                        var savedTags = dto.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct().ToList();
                        note.Tags = savedTags.Any() ? JsonSerializer.Serialize(savedTags) : null;
                        var oldTags = await _context.TagAssignments.Where(ta => ta.EntityId == note.Id.ToString() && ta.EntityType == "Note").ToListAsync();
                        // 只删除不再使用的关联、只新增缺少的关联，不依赖同一批次里先删后插的执行顺序。
                        var keptTagIds = new HashSet<Guid>();

                        if (dto.Tags.Any())
                        {
                            // tags.NormalizedName 是全局唯一索引（画廊、百科也共用这张表），必须按名称全局查找。
                            // 以前只在当前空间里查，别处已有同名标签时插入会撞唯一索引，整次保存被回滚且前端仍收到成功。
                            // 同一篇里重复的标签也要去重，否则 tag_assignments 的唯一索引同样会冲突。
                            var cleanTags = dto.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim())
                                .GroupBy(t => t.ToLower()).Select(g => g.First()).ToList();
                            var normalizedTags = cleanTags.Select(t => t.ToLower()).ToList();
                            var existingDbTags = (await _context.Tags.Where(t => normalizedTags.Contains(t.NormalizedName)).ToListAsync())
                                .GroupBy(t => t.NormalizedName, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                            foreach (var cleanName in cleanTags)
                            {
                                var normalizedName = cleanName.ToLower();
                                if (!existingDbTags.TryGetValue(normalizedName, out var tag))
                                {
                                    tag = new Tag { Id = Guid.NewGuid(), SpaceId = note.SpaceId, Name = cleanName, NormalizedName = normalizedName, CreatedAt = DateTime.UtcNow };
                                    _context.Tags.Add(tag);
                                    existingDbTags[normalizedName] = tag;
                                }
                                if (keptTagIds.Add(tag.Id) && !oldTags.Any(old => old.TagId == tag.Id))
                                    _context.TagAssignments.Add(new TagAssignment { Id = Guid.NewGuid(), TagId = tag.Id, EntityId = note.Id.ToString(), EntityType = "Note", CreatedAt = DateTime.UtcNow });
                            }
                        }
                        _context.TagAssignments.RemoveRange(oldTags.Where(old => !keptTagIds.Contains(old.TagId)));
                    }
                    // ========================================================================
                    // 🌟 3.5 投影 block_links（块级引用的边）
                    // ========================================================================
                    var sourceBlockIds = dto.Blocks?
                        .Select(b => b.Id)
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Cast<string>()
                        .ToList() ?? new List<string>();

                    // 3.5.1 删掉之前以这些块为 source 的边
                    if (sourceBlockIds.Any())
                    {
                        await _context.BlockLinks
                            .Where(l => sourceBlockIds.Contains(l.SourceBlockId))
                            .ExecuteDeleteAsync();
                    }

                    // 3.5.2 从 dto.Blocks 里重新解析
                    if (dto.Blocks != null)
                    {
                        var newBlockLinks = new List<BlockLink>();
                        var seenEdges = new HashSet<string>();

                        foreach (var b in dto.Blocks)
                        {
                            if (string.IsNullOrEmpty(b.Id)) continue;
                            var targets = ExtractBlockRefTargets(b.Data);
                            foreach (var tgt in targets)
                            {
                                if (tgt == b.Id) continue; // 自己引用自己跳过
                                var edgeKey = $"{b.Id}|{tgt}";
                                if (!seenEdges.Add(edgeKey)) continue;

                                newBlockLinks.Add(new BlockLink
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    SourceBlockId = b.Id,
                                    TargetBlockId = tgt,
                                    RelationType = "reference",
                                    CreatedAt = DateTime.UtcNow,
                                });
                            }
                        }

                        if (newBlockLinks.Any())
                        {
                            _context.BlockLinks.AddRange(newBlockLinks);
                        }
                    }
                    // ========================================================================
                    // 4. 投影 block_index
                    // ========================================================================
                    var indexNodeId = dto.NoteId.ToString();

                    await _context.BlockIndexes
                        .Where(x => x.NodeId == indexNodeId)
                        .ExecuteDeleteAsync();

                    if (dto.Blocks != null && dto.Blocks.Any())
                    {
                        int blockOrder = 0;
                        var seenIds = new HashSet<string>();

                        foreach (var b in dto.Blocks)
                        {
                            if (string.IsNullOrEmpty(b.Id) || !seenIds.Add(b.Id)) continue;

                            string? text = null;
                            string? latex = null;
                            string? assetId = null;

                            try
                            {
                                using var doc = JsonDocument.Parse(b.Data ?? "{}");
                                var root = doc.RootElement;

                                if (root.ValueKind == JsonValueKind.Object &&
                                    root.TryGetProperty("attrs", out var attrs) &&
                                    attrs.ValueKind == JsonValueKind.Object)
                                {
                                    if (attrs.TryGetProperty("latex", out var lx) &&
                                        lx.ValueKind == JsonValueKind.String)
                                        latex = lx.GetString();

                                    if (attrs.TryGetProperty("assetId", out var aid) &&
                                        aid.ValueKind == JsonValueKind.String)
                                        assetId = aid.GetString();
                                }

                                text = ExtractBlockText(root);
                            }
                            catch { }

                            _context.BlockIndexes.Add(new BlockIndex
                            {
                                Id = b.Id,
                                NodeId = indexNodeId,
                                BlockType = b.Type ?? "paragraph",
                                ParentBlockId = null,
                                SortOrder = blockOrder++,
                                TextContent = text,
                                Latex = latex,
                                AssetId = assetId,
                                UpdatedAt = DateTime.UtcNow,
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return NoteSaveResult.Saved;
                }
                catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException ||
                    ex.InnerException is MySqlConnector.MySqlException { Number: 1213 or 1062 })
                {
                    await transaction.RollbackAsync();
                    return ex.InnerException switch
                    {
                        MySqlConnector.MySqlException { Number: 1213 } => NoteSaveResult.DeadlockIgnored,
                        MySqlConnector.MySqlException { Number: 1062 } => NoteSaveResult.DuplicateIgnored,
                        _ => NoteSaveResult.ConcurrencyIgnored
                    };
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<(UserStats Stats, int UsedSpaces, int UsedNotes)> GetQuotaAsync(string userId)
        {
            var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == Guid.Parse(userId))
                ?? new UserStats { UserId = Guid.Parse(userId), MaxSpaces = 1, MaxNotes = 100 };
            var usedSpaces = await _context.Spaces.CountAsync(s => s.UserId == userId);
            var usedNotes = await _context.Notes.CountAsync(n =>
                _context.Spaces.Where(s => s.UserId == userId).Select(s => s.Id).Contains(n.SpaceId));
            return (stats, usedSpaces, usedNotes);
        }

        private static void CollectNoteReferences(string? data, Guid noteId, HashSet<Guid> targets)
        {
            if (string.IsNullOrWhiteSpace(data)) return;
            foreach (Match match in Regex.Matches(data, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"))
                if (Guid.TryParse(match.Value, out var target) && target != noteId) targets.Add(target);
        }

        /// <summary>
        /// 从 Block.Data JSON 里抽纯文本
        /// </summary>
        public static string ExtractTextFromBlockData(string? data)
        {
            if (string.IsNullOrEmpty(data)) return "";
            try
            {
                using var doc = JsonDocument.Parse(data);
                var sb = new System.Text.StringBuilder();
                WalkText(doc.RootElement, sb, true);
                return sb.ToString().Trim();
            }
            catch { return ""; }
        }

        private static string? ExtractBlockText(JsonElement node)
        {
            var sb = new System.Text.StringBuilder();
            WalkText(node, sb, false);
            var result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? null : result;
        }

        /// <summary>
        /// 递归遍历 JSON 节点，把所有 text 字段拼起来
        /// </summary>
        private static void WalkText(JsonElement node, System.Text.StringBuilder sb, bool includeTitle)
        {
            if (node.ValueKind != JsonValueKind.Object) return;

            if (node.TryGetProperty("text", out var t) &&
                t.ValueKind == JsonValueKind.String)
            {
                sb.Append(t.GetString());
                sb.Append(' ');
            }

            if (includeTitle && node.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                sb.Append(title.GetString());
                sb.Append(' ');
            }

            if (node.TryGetProperty("content", out var c) &&
                c.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in c.EnumerateArray())
                {
                    WalkText(child, sb, includeTitle);
                }
            }
        }

        /// <summary>
        /// 🌟 从 Block.Data JSON 里提取所有 spirit-link 指向的 blockId
        /// </summary>
        private static List<string> ExtractBlockRefTargets(string? data)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(data)) return result;
            try
            {
                using var doc = JsonDocument.Parse(data);
                WalkForBlockRefs(doc.RootElement, result);
            }
            catch { }
            return result;
        }

        private static void WalkForBlockRefs(JsonElement node, List<string> output)
        {
            if (node.ValueKind != JsonValueKind.Object) return;

            // 是 spirit-link 节点？
            if (node.TryGetProperty("type", out var t) &&
                t.ValueKind == JsonValueKind.String &&
                t.GetString() == "spirit-link")
            {
                if (node.TryGetProperty("attrs", out var attrs) &&
                    attrs.ValueKind == JsonValueKind.Object &&
                    attrs.TryGetProperty("blockId", out var bid) &&
                    bid.ValueKind == JsonValueKind.String)
                {
                    var b = bid.GetString();
                    if (!string.IsNullOrEmpty(b)) output.Add(b);
                }
            }

            // 递归
            if (node.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in c.EnumerateArray())
                    WalkForBlockRefs(child, output);
            }
        }

    }
}
