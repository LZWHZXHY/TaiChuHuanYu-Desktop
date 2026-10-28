from pathlib import Path
root=Path('E:/TaiChuHuanYu_DeskTop/TaiChuHuanYu-Desktop/TaiChuBackend_V2/TaiChuWeb_V2/TaiChuWeb_V2')
p=root/'Dtos/World/UpdateCardDto.cs';s=p.read_text(encoding='utf-8-sig').replace('    public class UpdateCardDto\n    {','    public class UpdateCardDto\n    {\n        // 可选以兼容既有调用；编辑器始终发送读取时的版本。\n        public DateTime? ExpectedUpdatedAt { get; set; }\n');Path('.tmp/world-draft-conflicts/' + p.name).write_text(s,encoding='utf-8')
p=root/'Models/World/WorldCard.cs';s=p.read_text(encoding='utf-8-sig').replace('        public DateTime UpdatedAt', '        [ConcurrencyCheck]\n        public DateTime UpdatedAt');s=s.replace('        [ForeignKey("ProjectId")]', '''        // MySQL datetime(6) 保存到微秒；返回的版本必须与数据库一致且严格递增。
        public void AdvanceVersion() => UpdatedAt = new DateTime(
            Math.Max(DateTime.UtcNow.Ticks / 10 * 10, UpdatedAt.Ticks / 10 * 10 + 10), DateTimeKind.Utc);

        [ForeignKey("ProjectId")]''');Path('.tmp/world-draft-conflicts/' + p.name).write_text(s,encoding='utf-8')
p=root/'Services/World/WorldCardService.cs';s=p.read_text(encoding='utf-8-sig');start=s.index('            var cacheKey = WorldCache.CardKey(cardId);');end=s.index('\n        }',start);s=s[:start]+'''            // 编辑冲突核对必须取得数据库当前内容，不能返回其他实例遗留的缓存。
            return EnsureProject(await GetCardByIdInternalAsync(cardId), projectId);'''+s[end:];s=s.replace('            await ApplyRelationsAsync(card, dto.Relations);\n            await _context.WorldCards.AddAsync(card);','            card.AdvanceVersion();\n            await ApplyRelationsAsync(card, dto.Relations);\n            await _context.WorldCards.AddAsync(card);');s=s.replace('            var affectedIds = card.OutRelations.Concat(card.InRelations)', '''            if (dto.ExpectedUpdatedAt.HasValue && dto.ExpectedUpdatedAt.Value.Ticks != card.UpdatedAt.Ticks)
                throw new WorldOperationException(WorldErrorKind.Conflict, "条目已被更新，请核对最新内容后再保存",
                    new Dictionary<string, object?> { ["code"] = "WORLD_CARD_CONFLICT" });

            var affectedIds = card.OutRelations.Concat(card.InRelations)''',1);s=s.replace('            card.UpdatedAt = DateTime.UtcNow;', '            card.AdvanceVersion();');Path('.tmp/world-draft-conflicts/' + p.name).write_text(s,encoding='utf-8')
p=root/'Controllers/World/WorldExceptionFilterAttribute.cs';s=p.read_text(encoding='utf-8-sig').replace('using Microsoft.AspNetCore.Mvc;', 'using Microsoft.EntityFrameworkCore;\nusing Microsoft.AspNetCore.Mvc;');s=s.replace('        else if (FailureMessage != null)', '''        else if (context.Exception is DbUpdateConcurrencyException)
            context.Result = new ConflictObjectResult(new { code = "WORLD_CARD_CONFLICT", message = "条目已被更新，请核对最新内容后再保存" });
        else if (FailureMessage != null)''');Path('.tmp/world-draft-conflicts/' + p.name).write_text(s,encoding='utf-8')
p=root/'Services/World/WorldRelationService.cs';s=p.read_text(encoding='utf-8-sig').replace('.Select(c => new { c.ProjectId, OwnerId = c.Project != null ? c.Project.OwnerId : Guid.Empty })', '.Include(c => c.Project)');s=s.replace('sourceInfo.OwnerId', 'sourceInfo.Project?.OwnerId ?? Guid.Empty');s=s.replace('            await _context.WorldRelations.AddAsync(relation);', '            sourceInfo.AdvanceVersion();\n            await _context.WorldRelations.AddAsync(relation);');s=s.replace('            _context.WorldRelations.Remove(relation);','            relation.SourceCard.AdvanceVersion();\n            _context.WorldRelations.Remove(relation);');Path('.tmp/world-draft-conflicts/' + p.name).write_text(s,encoding='utf-8')

