using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.DTOs.Activity;

// 创建帖子请求
public class CreatePostDto
{
    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}

// 创建回复请求
public class CreateReplyDto
{
    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;
}

// 帖子响应
public class PostResponseDto
{
    public int Id { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ReplyCount { get; set; }
    public List<ReplyResponseDto> Replies { get; set; } = new();
}

// 回复响应
public class ReplyResponseDto
{
    public int Id { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}