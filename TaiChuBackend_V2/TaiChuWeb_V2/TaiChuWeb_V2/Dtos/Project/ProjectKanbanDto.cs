namespace TaiChuWeb_V2.Dtos.Project
{
    public class CreateCategoryDto
    {
        public string Name { get; set; }
        public string ColorCode { get; set; }
    }

    public class UpdateCategoryDto
    {
        public string? Name { get; set; }
        public string? ColorCode { get; set; }
    }

    

    public class DragMoveTaskDto
    {
        public string? TargetCategoryId { get; set; }
        public double? PrevSortOrder { get; set; }
        public double? NextSortOrder { get; set; }
    }

    // 🌟 配合详情超级弹窗的更新 DTO
    public class UpdateTaskDto
    {
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? CategoryId { get; set; }
        public string? AssigneeId { get; set; }
        public int? Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Tags { get; set; }
    }
}
