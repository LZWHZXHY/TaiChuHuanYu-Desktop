namespace TaiChuWeb_V2.Dtos.Project
{
    public class CreateTaskDto
    {
        public string Title { get; set; }
        public int Status { get; set; } = 0;
        public string? CategoryId { get; set; }
    }
}
