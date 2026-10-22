using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.LingMai;

public class CreateNoteCommentDto
{
    [Required, StringLength(1000)]
    public string Content { get; set; } = "";
    public Guid? ParentId { get; set; }
}

public class NoteCommentDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string? Avatar { get; set; }
    public string Content { get; set; } = "";
    public Guid? ParentId { get; set; }
    public string? ReplyToAuthor { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool CanDelete { get; set; }
}
