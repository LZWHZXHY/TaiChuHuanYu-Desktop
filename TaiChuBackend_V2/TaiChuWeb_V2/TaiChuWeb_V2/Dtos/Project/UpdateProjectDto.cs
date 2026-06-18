namespace TaiChuWeb_V2.Dtos.Project
{
    public class UpdateProjectDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsPublic { get; set; }
        public int? JoinPolicy { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Status { get; set; }

        public string? CoverUrl { get; set; }
    }
}
