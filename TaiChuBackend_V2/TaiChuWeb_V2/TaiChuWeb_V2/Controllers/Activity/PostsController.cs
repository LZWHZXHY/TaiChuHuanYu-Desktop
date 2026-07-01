using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.DTOs.Activity;
using PostModel = TaiChuWeb_V2.Models.Activity.Post;   // 别名
using ReplyModel = TaiChuWeb_V2.Models.Activity.Reply; // 别名

namespace TaiChuWeb_V2.Controllers.Activity;

[ApiController]
[Route("api/activities/{activityId}/posts")]
[Authorize]
public class PostsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PostsController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("用户未认证");
        return Guid.Parse(userIdClaim);
    }

    [HttpGet]
    public async Task<IActionResult> GetPosts(int activityId)
    {
        var posts = await _context.Posts
            .Include(p => p.Author)
            .Include(p => p.Replies)
                .ThenInclude(r => r.Author)
            .Where(p => p.ActivityId == activityId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PostResponseDto
            {
                Id = p.Id,
                Author = p.Author.Username,
                Content = p.Content,
                CreatedAt = p.CreatedAt,
                ReplyCount = p.Replies.Count,
                Replies = p.Replies.OrderBy(r => r.CreatedAt).Select(r => new ReplyResponseDto
                {
                    Id = r.Id,
                    Author = r.Author.Username,
                    Content = r.Content,
                    CreatedAt = r.CreatedAt
                }).ToList()
            })
            .ToListAsync();

        return Ok(posts);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePost(int activityId, CreatePostDto dto)
    {
        var userId = GetCurrentUserId();

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId);

        if (activity == null)
            return NotFound("活动不存在");

        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.ActivityId == activityId && m.UserId == userId);

        if (member == null)
            return BadRequest("只有活动成员可以发帖");

        var post = new PostModel // 使用别名
        {
            ActivityId = activityId,
            AuthorId = userId,
            Content = dto.Content,
            CreatedAt = DateTime.UtcNow
        };

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPost), new { activityId, postId = post.Id }, new PostResponseDto
        {
            Id = post.Id,
            Author = (await _context.Users.FindAsync(userId))?.Username ?? string.Empty,
            Content = post.Content,
            CreatedAt = post.CreatedAt,
            ReplyCount = 0,
            Replies = new List<ReplyResponseDto>()
        });
    }

    [HttpGet("{postId}")]
    public async Task<IActionResult> GetPost(int activityId, int postId)
    {
        var post = await _context.Posts
            .Include(p => p.Author)
            .Include(p => p.Replies)
                .ThenInclude(r => r.Author)
            .FirstOrDefaultAsync(p => p.Id == postId && p.ActivityId == activityId);

        if (post == null)
            return NotFound("帖子不存在");

        return Ok(new PostResponseDto
        {
            Id = post.Id,
            Author = post.Author.Username,
            Content = post.Content,
            CreatedAt = post.CreatedAt,
            ReplyCount = post.Replies.Count,
            Replies = post.Replies.OrderBy(r => r.CreatedAt).Select(r => new ReplyResponseDto
            {
                Id = r.Id,
                Author = r.Author.Username,
                Content = r.Content,
                CreatedAt = r.CreatedAt
            }).ToList()
        });
    }

    [HttpPost("{postId}/replies")]
    public async Task<IActionResult> CreateReply(int activityId, int postId, CreateReplyDto dto)
    {
        var userId = GetCurrentUserId();

        var post = await _context.Posts
            .FirstOrDefaultAsync(p => p.Id == postId && p.ActivityId == activityId);

        if (post == null)
            return NotFound("帖子不存在");

        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.ActivityId == activityId && m.UserId == userId);

        if (member == null)
            return BadRequest("只有活动成员可以回复");

        var reply = new ReplyModel // 使用别名
        {
            PostId = postId,
            AuthorId = userId,
            Content = dto.Content,
            CreatedAt = DateTime.UtcNow
        };

        _context.Replies.Add(reply);
        await _context.SaveChangesAsync();

        return Ok(new ReplyResponseDto
        {
            Id = reply.Id,
            Author = (await _context.Users.FindAsync(userId))?.Username ?? string.Empty,
            Content = reply.Content,
            CreatedAt = reply.CreatedAt
        });
    }

    [HttpDelete("{postId}")]
    public async Task<IActionResult> DeletePost(int activityId, int postId)
    {
        var userId = GetCurrentUserId();

        var post = await _context.Posts
            .FirstOrDefaultAsync(p => p.Id == postId && p.ActivityId == activityId);

        if (post == null)
            return NotFound("帖子不存在");

        if (post.AuthorId != userId)
            return Forbid("只能删除自己的帖子");

        _context.Posts.Remove(post);
        await _context.SaveChangesAsync();

        return Ok(new { message = "帖子已删除" });
    }

    [HttpDelete("replies/{replyId}")]
    public async Task<IActionResult> DeleteReply(int activityId, int replyId)
    {
        var userId = GetCurrentUserId();

        var reply = await _context.Replies
            .Include(r => r.Post)
            .FirstOrDefaultAsync(r => r.Id == replyId && r.Post.ActivityId == activityId);

        if (reply == null)
            return NotFound("回复不存在");

        if (reply.AuthorId != userId)
            return Forbid("只能删除自己的回复");

        _context.Replies.Remove(reply);
        await _context.SaveChangesAsync();

        return Ok(new { message = "回复已删除" });
    }
}