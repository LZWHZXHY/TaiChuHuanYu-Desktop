import fs from 'node:fs';import path from 'node:path';
const base='.tmp/club-joint-battle/backend/';
const p='TaiChuWeb_V2/TaiChuWeb_V2/Controllers/LingMai/LingMaiPublishController.cs';
let code=fs.readFileSync('../../TaiChuBackend_V2/'+p,'utf8').replace(/\r\n/g,'\n');
fs.mkdirSync(path.dirname(base+p),{recursive:true});if(!fs.existsSync(base+p+'.before'))fs.writeFileSync(base+p+'.before',code);
code=code.replace('                        var existingPublish = await _context.PublishedNotes',`                        if (note.Type == NoteTypes.Doc)
                        {
                            var noteId = id.ToString();
                            var links = await _context.ProjectDocuments.Where(d => d.NoteId == noteId).ToListAsync();
                            _context.ProjectDocuments.RemoveRange(links);
                            note.UpdatedAt = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            return Ok(new { success = true, message = "已取消所有项目分享" });
                        }

                        var existingPublish = await _context.PublishedNotes`);
fs.writeFileSync(base+p,code);
const checks='tests/LingMai.Regression/JointBattleChecks.cs';let tests=fs.readFileSync(base+checks,'utf8');
tests=tests.replace('        return checks;',`        projectDb.SpaceRows.Rows.Add(new() { Id = note.SpaceId, UserId = owner.ToString() });
        var publication = new TaiChuWeb_V2.Controllers.LingMai.LingMaiPublishController(projectDb, new[] { publisher }) { ControllerContext = Login(guest) };
        Check(await publication.UnpublishNote(note.Id) is ForbidResult && documents.Rows.Count == 1, "reader cannot withdraw author's shared document");
        publication.ControllerContext = Login(owner);
        Check(await publication.UnpublishNote(note.Id) is OkObjectResult && documents.Rows.Count == 0 && note.IsPublic, "unshare removes project links without changing public status");
        reader.ControllerContext = Login(owner);
        Check(await reader.GetProjectDocument(project.Id, note.Id) is NotFoundObjectResult, "withdrawn document is no longer readable from project");
        return checks;`);fs.writeFileSync(base+checks,tests);
