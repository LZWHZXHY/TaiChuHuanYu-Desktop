namespace TaiChuWeb_V2.Dtos.Project
{
    public class CreateCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string? ColorCode { get; set; } = "#1a1a1a";
        public int CategoryType { get; set; } = 0; // 🌟 允许创建时指定类型
    }

    public class UpdateCategoryDto
    {
        public string? Name { get; set; }
        public string? ColorCode { get; set; }
        public int? CategoryType { get; set; } // 🌟 允许修改时更新类型
    }



    public class DragMoveTaskDto
    {
        public string? TargetCategoryId { get; set; }
        public double? PrevSortOrder { get; set; }
        public double? NextSortOrder { get; set; }
    }

    public class UpdateTaskDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? Priority { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Tags { get; set; }
        public string? CategoryId { get; set; }
        public string? AssigneeId { get; set; }

        // 🌟 补齐以下 3 个量化指标属性，彻底解决 CS1061 报错
        public int? Points { get; set; }
        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
    }
}
