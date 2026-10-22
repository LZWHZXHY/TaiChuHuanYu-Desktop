import fs from 'node:fs';
import path from 'node:path';
const p='TaiChuWeb_V2/TaiChuWeb_V2/Controllers/Projects/ProjectController.cs';
const source=fs.readFileSync('../../TaiChuBackend_V2/'+p,'utf8').replace(/\r\n/g,'\n');
const dest='.tmp/club-joint-battle/backend/'+p;
fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest+'.before',source);
const endpoint=`        // 项目内的只读文档入口，不扩大个人笔记编辑接口的访问范围。
        [HttpGet("{projectId}/documents/{noteId:guid}")]
        public async Task<IActionResult> GetProjectDocument(string projectId, Guid noteId)
        {
            if (!await IsMember(projectId)) return Forbid();
            var id = noteId.ToString();
            if (!await _context.ProjectDocuments.AnyAsync(d => d.ProjectId == projectId && d.NoteId == id))
                return NotFound(new { message = "文档未分享至此项目或已取消分享" });
            var note = await _context.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == noteId && n.Status != 2);
            if (note == null) return NotFound(new { message = "文档不存在或已删除" });
            var blocks = await _context.Blocks.AsNoTracking()
                .Where(b => b.OwnerId == id && b.OwnerType == note.Type)
                .OrderBy(b => b.SortOrder)
                .Select(b => new { id = b.Id, type = b.Type, data = b.Data, sortOrder = b.SortOrder })
                .ToListAsync();
            return Ok(new { id = note.Id, title = note.Title, type = note.Type, blocks });
        }

`;
fs.writeFileSync(dest,source.replace('        [HttpGet("{projectId}/documents")]',endpoint+'        [HttpGet("{projectId}/documents")]'));
