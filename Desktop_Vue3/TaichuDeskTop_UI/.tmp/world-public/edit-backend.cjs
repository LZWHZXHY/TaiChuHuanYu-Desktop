const fs = require('fs');
function edit(file, fn) { const p = '.tmp/world-public/backend/'+file; fs.writeFileSync(p,fn(fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n'))); }
edit('WorldControllerBase.cs',s=>s.replace('    protected Guid CurrentUserId =>','    // 匿名读取使用空身份；写操作继续使用严格认证的 CurrentUserId。\n    protected Guid ReaderUserId => User.Identity?.IsAuthenticated == true\n        && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;\n\n    protected Guid CurrentUserId =>'));
const methods = {
  'WorldProjectsController.cs':['GetProject'],
  'WorldCardsController.cs':['GetCards','GetTimeline','GetCard','GetBacklinks'],
  'WorldRelationsController.cs':['GetRelations','GetProjectRelations'],
  'WorldSearchController.cs':['Search'],
};
for (const [file,names] of Object.entries(methods)) edit(file,s=>{
  for (const name of names) {
    const pattern = new RegExp('(    \\[HttpGet[^\\n]*\\]\\n)(    public async Task<IActionResult> '+name+'\\([^]*?)(?=\\n    \\[|\\n})');
    if (!pattern.test(s)) throw Error('Missing '+name);
    s=s.replace(pattern,(_,attr,body)=>attr+'    [AllowAnonymous]\n'+body.replaceAll('CurrentUserId','ReaderUserId'));
  }
  return s;
});
edit('WorldCardsController.cs',s=>s.replace('GetBacklinksAsync(cardId, ReaderUserId)','GetBacklinksAsync(cardId, ReaderUserId, projectId)'));
edit('IWorldCardService.cs',s=>s.replace('GetBacklinksAsync(Guid cardId, Guid userId);','GetBacklinksAsync(Guid cardId, Guid userId, Guid? projectId = null);'));
edit('WorldCardService.cs',s=>s.replace('GetBacklinksAsync(Guid cardId, Guid userId)','GetBacklinksAsync(Guid cardId, Guid userId, Guid? projectId = null)')
  .replace('if (target == null) return new List<CardSummaryDto>();','if (target == null || (projectId.HasValue && target.ProjectId != projectId.Value)) return new List<CardSummaryDto>();')
  .replace('.Where(p => p.OwnerId == userId || p.IsPublic)','.ReadableBy(userId)'));
