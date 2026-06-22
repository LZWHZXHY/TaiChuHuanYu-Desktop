using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Project;

namespace TaiChuWeb_V2.Services.Publish
{
    public class DocPublishHandler : ILingMaiPublishHandler
    {
        private readonly AppDbContext _context;

        // 🌟 严格对齐前端传过来的 type: "doc"
        public string SupportType => "doc";

        public DocPublishHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId, string? projectId = null)
        {
            // 1. 强校验：发布项目文档必须带上目标项目ID
            if (string.IsNullOrWhiteSpace(projectId))
            {
                return new BadRequestObjectResult(new { message = "发布项目文档必须指定归属的项目 ID" });
            }

            return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
            {
                // 2. 捞出灵脉草稿实体
                var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
                if (note == null) return new NotFoundObjectResult(new { message = "未找到该灵脉草稿" });

                // 3. 校验目标项目是否存在
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                if (project == null) return new NotFoundObjectResult(new { message = "目标协作项目不存在" });

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 4. 检查是否已经关联过（防止重复点击发布导致主键冲突）
                    bool alreadyLinked = await _context.ProjectDocuments
                        .AnyAsync(pd => pd.ProjectId == projectId && pd.NoteId == noteId.ToString());

                    if (!alreadyLinked)
                    {
                        // 🌟 核心：写入 ProjectDocument 关联表
                        _context.ProjectDocuments.Add(new ProjectDocument
                        {
                            ProjectId = projectId,
                            NoteId = noteId.ToString(),
                            PinnedByUserId = userId,
                            PinnedAt = DateTime.UtcNow
                        });
                    }

                    // 5. 更改原始草稿的公开状态
                    note.IsPublic = true;
                    note.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new OkObjectResult(new { success = true, projectId = projectId });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return new BadRequestObjectResult(new { message = $"项目文档发布失败: {ex.Message}" });
                }
            });
        }
    }
}