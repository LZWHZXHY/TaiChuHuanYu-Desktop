from pathlib import Path
import json, difflib
patches=[]
root=Path('E:/TaiChuHuanYu_DeskTop/TaiChuHuanYu-Desktop/TaiChuBackend_V2/TaiChuWeb_V2/TaiChuWeb_V2')
paths=['Controllers/World/WorldCardsController.cs','Services/World/IWorldCardService.cs','Services/World/WorldCardService.cs']
backup={str(root/p):(root/p).read_text(encoding='utf-8-sig') for p in paths}

p=root/paths[0];s=backup[str(p)];pos=s.index('    [HttpGet("timeline")]');s=s[:pos]+'''    [HttpGet("references")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReferences(Guid projectId) =>
        Ok(await cardService.GetReferencesAsync(projectId, ReaderUserId));

    [HttpPost("batch")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBatch(Guid projectId, [FromBody] Guid[] ids)
    {
        if (ids.Length > 100) return BadRequest(new { message = "每批最多读取 100 个条目" });
        return Ok(await cardService.GetCardsByIdsAsync(projectId, ids, ReaderUserId));
    }

'''+s[pos:];patches.append('*** Update File: '+str(p)+'\n'+''.join(list(difflib.unified_diff(backup[str(p)].splitlines(True),s.splitlines(True)))[2:])) if p.name != 'WorldCardsController.cs' else None
p=root/paths[1];s=backup[str(p)].replace('        Task<IEnumerable<TimelineItemDto>> GetTimelineAsync', '        Task<Dictionary<Guid, List<Guid>>> GetReferencesAsync(Guid projectId, Guid userId);\n\n        Task<IEnumerable<TimelineItemDto>> GetTimelineAsync');patches.append('*** Update File: '+str(p)+'\n'+''.join(list(difflib.unified_diff(backup[str(p)].splitlines(True),s.splitlines(True)))[2:])) if p.name != 'WorldCardsController.cs' else None
p=root/paths[2];s=backup[str(p)];pos=s.index('        public async Task<IEnumerable<CardResponseDto>> GetCardsByIdsAsync');end=s.index('        // ============================================================',pos);part=s[pos:end];a=part.index('            // 验证项目权限');b=part.index('            // ✅',a);part=part[:a]+'            await WorldAccess.RequireReadableProjectAsync(_context, projectId, userId);\n\n'+part[b:];part=part.replace('.AsNoTracking()', '.AsNoTracking().AsSplitQuery()');s=s[:pos]+part+s[end:];pos=s.index('        public async Task<IEnumerable<TimelineItemDto>> GetTimelineAsync');s=s[:pos]+'''        public async Task<Dictionary<Guid, List<Guid>>> GetReferencesAsync(Guid projectId, Guid userId)
        {
            await WorldAccess.RequireReadableProjectAsync(_context, projectId, userId);
            var rows = await _context.WorldCards.AsNoTracking().Where(c => c.ProjectId == projectId)
                .Select(c => new { c.Id, c.Description, c.ContentBlocks }).ToListAsync();
            var allowed = rows.Select(c => c.Id).ToHashSet();
            var result = new Dictionary<Guid, List<Guid>>();
            foreach (var row in rows)
            {
                var ids = new HashSet<Guid>();
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(row.Description ?? "", @"\\{CARD:([^}]+)\\}"))
                    if (Guid.TryParse(match.Groups[1].Value, out var id)) ids.Add(id);
                try
                {
                    using var blocks = JsonDocument.Parse(row.ContentBlocks ?? "[]");
                    if (blocks.RootElement.ValueKind == JsonValueKind.Array)
                        foreach (var block in blocks.RootElement.EnumerateArray())
                            if (block.ValueKind == JsonValueKind.Object && block.TryGetProperty("cardId", out var value)
                                && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id)) ids.Add(id);
                }
                catch (JsonException) { /* 旧数据损坏时仍保留正文中的有效引用。 */ }
                ids.Remove(row.Id);
                var targets = ids.Where(allowed.Contains).OrderBy(id => id).ToList();
                if (targets.Count > 0) result[row.Id] = targets;
            }
            return result;
        }

'''+s[pos:];patches.append('*** Update File: '+str(p)+'\n'+''.join(list(difflib.unified_diff(backup[str(p)].splitlines(True),s.splitlines(True)))[2:])) if p.name != 'WorldCardsController.cs' else None
print('*** Begin Patch\n'+''.join(patches)+'*** End Patch')
