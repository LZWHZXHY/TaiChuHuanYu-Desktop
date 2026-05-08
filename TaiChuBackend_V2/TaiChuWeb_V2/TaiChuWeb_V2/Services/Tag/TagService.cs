using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Tag;
using TaiChuWeb_V2.Services.Tags;

namespace TaiChuWeb_V2.Services
{
    public class TagService : ITagService
    {
        private readonly AppDbContext _context;

        public TagService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Tag>> GetTagsAsync(string entityType, string entityId)
        {
            return await _context.TagAssignments
                .Where(ta => ta.EntityType == entityType && ta.EntityId == entityId)
                .Select(ta => ta.Tag!)
                .ToListAsync();
        }

        public async Task<Dictionary<string, List<Tag>>> GetTagsForEntitiesAsync(string entityType, IEnumerable<string> entityIds)
        {
            if (entityIds == null || !entityIds.Any())
                return new Dictionary<string, List<Tag>>();

            var assignments = await _context.TagAssignments
                .Where(ta => ta.EntityType == entityType && entityIds.Contains(ta.EntityId))
                .Include(ta => ta.Tag)
                .ToListAsync();

            return assignments
                .Where(ta => ta.Tag != null)
                .GroupBy(ta => ta.EntityId)
                .ToDictionary(g => g.Key, g => g.Select(ta => ta.Tag!).ToList());
        }

        public async Task UpdateTagsAsync(string entityType, string entityId, IEnumerable<string> tagNames)
        {
            if (tagNames == null) tagNames = new List<string>();

            // 1. 规范化输入
            var normalizedInputs = tagNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            // 2. 找出数据库中已存在的 Tag 记录
            var existingTags = await _context.Tags
                .Where(t => normalizedInputs.Contains(t.NormalizedName))
                .ToListAsync();

            // 3. 找出不存在的 Tag 名称并创建
            var missingTagNames = normalizedInputs
                .Except(existingTags.Select(t => t.NormalizedName))
                .ToList();

            if (missingTagNames.Any())
            {
                foreach (var newTagName in missingTagNames)
                {
                    // 尽量保留用户输入时的原样大小写（取第一个匹配的）
                    var originalName = tagNames.First(n => n.Trim().Equals(newTagName, StringComparison.OrdinalIgnoreCase));
                    var newTag = new Tag
                    {
                        Id = Guid.NewGuid(),
                        Name = originalName.Trim(),
                        NormalizedName = newTagName
                    };
                    _context.Tags.Add(newTag);
                    existingTags.Add(newTag);
                }
                await _context.SaveChangesAsync();
            }

            // 4. 读取该实体当前的标签关联
            var currentAssignments = await _context.TagAssignments
                .Where(ta => ta.EntityType == entityType && ta.EntityId == entityId)
                .ToListAsync();

            var targetTagIds = existingTags.Select(t => t.Id).ToList();
            var currentTagIds = currentAssignments.Select(ta => ta.TagId).ToList();

            // 5. 计算需要移除和新增的关联
            var removeAssignments = currentAssignments.Where(ta => !targetTagIds.Contains(ta.TagId));
            _context.TagAssignments.RemoveRange(removeAssignments);

            var addTagIds = targetTagIds.Except(currentTagIds);
            foreach (var tagId in addTagIds)
            {
                _context.TagAssignments.Add(new TagAssignment
                {
                    Id = Guid.NewGuid(),
                    TagId = tagId,
                    EntityId = entityId,
                    EntityType = entityType
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task RemoveTagsFromEntityAsync(string entityType, string entityId)
        {
            var assignments = await _context.TagAssignments
                .Where(ta => ta.EntityType == entityType && ta.EntityId == entityId)
                .ToListAsync();

            if (assignments.Any())
            {
                _context.TagAssignments.RemoveRange(assignments);
                await _context.SaveChangesAsync();
            }
        }
    }
}