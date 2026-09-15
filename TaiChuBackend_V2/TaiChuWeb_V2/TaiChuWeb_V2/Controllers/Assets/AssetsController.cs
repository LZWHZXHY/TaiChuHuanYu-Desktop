using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.FinalNodes;

namespace TaiChuWeb_V2.Controllers.Assets
{
    public class RegisterAssetDto
    {
        public string Url { get; set; } = string.Empty;
        public string? StorageKey { get; set; }
        public string? FileName { get; set; }
        public string? MimeType { get; set; }
        public long Size { get; set; }
        public string Kind { get; set; } = "image";
        public int? Width { get; set; }
        public int? Height { get; set; }
        public Guid? SpaceId { get; set; }
    }

    [ApiController]
    [Route("api/assets")]
    public class AssetsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        public AssetsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 前端已经直传 COS 成功后，调这里登记元数据，拿到 assetId
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterAssetDto dto)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
            if (string.IsNullOrEmpty(dto.Url)) return BadRequest(new { message = "Url 不能为空" });

            // 用 URL 去重（同一用户同一文件重复登记时返回已有的）
            var existing = await _context.Assets
                .FirstOrDefaultAsync(a => a.Url == dto.Url && a.OwnerId == CurrentUserId);
            if (existing != null)
            {
                return Ok(new { id = existing.Id, url = existing.Url, kind = existing.Kind });
            }

            var asset = new Asset
            {
                Id = Guid.NewGuid().ToString(),
                OwnerId = CurrentUserId,
                SpaceId = dto.SpaceId,
                Kind = dto.Kind,
                FileName = dto.FileName,
                MimeType = dto.MimeType,
                Size = dto.Size,
                StorageKey = dto.StorageKey ?? dto.Url,  // 兜底，防止唯一索引报 null
                Url = dto.Url,
                Width = dto.Width,
                Height = dto.Height,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Assets.Add(asset);
            await _context.SaveChangesAsync();

            return Ok(new { id = asset.Id, url = asset.Url, kind = asset.Kind });
        }

        /// <summary>
        /// 按 assetId 拿元数据
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == id);
            if (asset == null) return NotFound();
            if (asset.OwnerId != CurrentUserId) return Forbid();

            return Ok(new
            {
                id = asset.Id,
                url = asset.Url,
                kind = asset.Kind,
                fileName = asset.FileName,
                mimeType = asset.MimeType,
                size = asset.Size,
                width = asset.Width,
                height = asset.Height,
            });
        }
    }
}