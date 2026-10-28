using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.World;

public class StartWorldRestoreDto
{
    public Guid SourceProjectId { get; set; }
    [Required, RegularExpression("^[a-f0-9]{64}$")] public string Fingerprint { get; set; } = "";
    [Required, MaxLength(255)] public string FileName { get; set; } = "";
    public bool NewProject { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(50)] public string DefaultType { get; set; } = "";
    public bool PlainText { get; set; }
    [Range(0, int.MaxValue)] public int CardCount { get; set; }
    public List<RestoreImageDto> Images { get; set; } = [];
}

public class RestoreImageDto
{
    // 文件名由原图 SHA-256 和已校验的扩展名组成，不能传入任意 COS 路径。
    [Required, RegularExpression("^[a-f0-9]{64}\\.(png|jpg|gif|webp|avif|bmp|ico|svg)$")]
    public string Name { get; set; } = "";
    [Range(1, 32 * 1024 * 1024)] public int Size { get; set; }
}

public class CommitWorldRestoreDto
{
    public string? Description { get; set; }
    public List<CreateCardDto> Cards { get; set; } = [];
}

public record WorldRestorePlan(StartWorldRestoreDto Source, Guid[] CardIds);
public record WorldRestoreStatus(Guid Id, string State, Guid TargetProjectId, WorldRestorePlan Plan,
    Dictionary<string, string> Images, DateTime UpdatedAt);
