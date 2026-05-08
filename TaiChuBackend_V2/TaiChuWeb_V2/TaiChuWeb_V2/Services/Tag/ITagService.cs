using TaiChuWeb_V2.Models.Tag; // 确保引入了 Tag 实体类所在的命名空间

namespace TaiChuWeb_V2.Services.Tags // 🌟 命名空间末尾改为 Tags，避开与类名 Tag 重名
{
    public interface ITagService
    {
        // 获取某个实体的所有标签
        Task<List<Models.Tag.Tag>> GetTagsAsync(string entityType, string entityId);

        // 批量获取多个实体的标签（用于列表页高效加载，避免 N+1 查询问题）
        Task<Dictionary<string, List<Models.Tag.Tag>>> GetTagsForEntitiesAsync(string entityType, IEnumerable<string> entityIds);

        // 更新某个实体的标签
        Task UpdateTagsAsync(string entityType, string entityId, IEnumerable<string> tagNames);

        // 删除某个实体的所有标签关联（用于实体被删除时的联级清理）
        Task RemoveTagsFromEntityAsync(string entityType, string entityId);
    }
}