using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers
{
    public class SaveAiConfigDto
    {
        public string BaseUrl { get; set; } = "https://api.deepseek.com";
        public string Model { get; set; } = "deepseek-chat";
        /// <summary>留空或为脱敏值时不更新；填新值则覆盖。</summary>
        public string? ApiKey { get; set; }
    }

    public class OrganizeWorldDto
    {
        public Guid ProjectId { get; set; }
    }

    public class ImportWorldDto
    {
        public Guid ProjectId { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class ImportCardItemDto
    {
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? SubType { get; set; }
        public string? Description { get; set; }
        public List<AttributeDto> Attributes { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    public class ConfirmImportDto
    {
        public Guid ProjectId { get; set; }
        public List<ImportCardItemDto> Cards { get; set; } = new();
    }

    /// <summary>用户自带 AI（BYOK）配置 + 世界观一键整理代理。</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AiConfigController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IWorldCardService _worldCardService;

        public AiConfigController(AppDbContext context, IConfiguration config, IHttpClientFactory httpFactory, IWorldCardService worldCardService)
        {
            _context = context;
            _config = config;
            _httpFactory = httpFactory;
            _worldCardService = worldCardService;
        }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        private string EncryptionSecret => _config["Ai:EncryptionKey"] ?? _config["Jwt:Key"] ?? "TaichuAiDefaultSecret";

        // ===== 配置查询（key 脱敏） =====
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            var cfg = await _context.UserAiConfigs.FirstOrDefaultAsync(c => c.UserId == CurrentUserId.Value);
            if (cfg == null)
                return Ok(new { baseUrl = "https://api.deepseek.com", model = "deepseek-chat", maskedKey = "", hasKey = false });

            var plainKey = AiKeyCrypto.Decrypt(cfg.EncryptedApiKey, EncryptionSecret);
            return Ok(new { baseUrl = cfg.BaseUrl, model = cfg.Model, maskedKey = AiKeyCrypto.Mask(plainKey), hasKey = !string.IsNullOrEmpty(plainKey) });
        }

        // ===== 保存配置 =====
        [HttpPut]
        public async Task<IActionResult> Save([FromBody] SaveAiConfigDto dto)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            if (string.IsNullOrWhiteSpace(dto.BaseUrl)) return BadRequest(new { message = "接口地址不能为空" });

            var cfg = await _context.UserAiConfigs.FirstOrDefaultAsync(c => c.UserId == CurrentUserId.Value);
            if (cfg == null)
            {
                cfg = new UserAiConfig { UserId = CurrentUserId.Value };
                _context.UserAiConfigs.Add(cfg);
            }

            cfg.BaseUrl = dto.BaseUrl.Trim();
            cfg.Model = string.IsNullOrWhiteSpace(dto.Model) ? "deepseek-chat" : dto.Model.Trim();
            cfg.UpdatedAt = DateTime.UtcNow;

            // 仅当用户提交了新的、未脱敏的 key 时才更新
            if (!string.IsNullOrWhiteSpace(dto.ApiKey) && !dto.ApiKey.Contains("****"))
            {
                cfg.EncryptedApiKey = AiKeyCrypto.Encrypt(dto.ApiKey.Trim(), EncryptionSecret);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "AI 配置已保存" });
        }

        // ===== 一键整理世界观 =====
        [HttpPost("organize-world")]
        public async Task<IActionResult> OrganizeWorld([FromBody] OrganizeWorldDto dto)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            // 公开项目任何人可总结；私有项目仅作者
            if (!await _context.WorldProjects.AsNoTracking().AnyAsync(p => p.Id == dto.ProjectId && (p.IsPublic || p.OwnerId == CurrentUserId.Value)))
                return NotFound(new { message = "项目不存在或无权访问" });

            var cfg = await _context.UserAiConfigs.FirstOrDefaultAsync(c => c.UserId == CurrentUserId.Value);
            if (cfg == null || string.IsNullOrEmpty(cfg.EncryptedApiKey))
                return BadRequest(new { message = "请先在个人中心配置你的 AI（接口地址、Key、模型）" });

            var apiKey = AiKeyCrypto.Decrypt(cfg.EncryptedApiKey, EncryptionSecret);
            if (string.IsNullOrEmpty(apiKey))
                return BadRequest(new { message = "AI Key 解密失败，请重新配置" });

            var cards = await _context.WorldCards
                .Where(c => c.ProjectId == dto.ProjectId)
                .OrderBy(c => c.Type).ThenBy(c => c.Title)
                .Select(c => new { c.Title, c.Type, c.SubType, c.Description, c.Attributes, c.Tags })
                .ToListAsync();

            if (cards.Count == 0) return BadRequest(new { message = "该项目下还没有卡片内容" });

            var prompt = BuildPrompt(cards);
            try
            {
                var result = await CallAiAsync(cfg.BaseUrl, apiKey, cfg.Model, prompt);
                return Ok(new { result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "AI 调用失败：" + ex.Message });
            }
        }

        // ===== AI 导入设定：解析文字 → 结构化卡片（预览） =====
        [HttpPost("import-world")]
        public async Task<IActionResult> ImportWorld([FromBody] ImportWorldDto dto)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            // 导入只允许自己的项目
            if (!await _context.WorldProjects.AsNoTracking().AnyAsync(p => p.Id == dto.ProjectId && p.OwnerId == CurrentUserId.Value))
                return NotFound(new { message = "只能导入到自己的项目" });
            if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest(new { message = "请先粘贴设定文字" });

            var (apiKey, baseUrl, model, err) = await GetAiCredentialsAsync(CurrentUserId.Value);
            if (err != null) return BadRequest(new { message = err });

            var prompt = "请从以下设定文字中提取所有可独立成「世界观卡片」的实体（人物、地点、物品、势力、种族、概念、事件等）。\n"
                + "输出一个 JSON 数组，不要任何多余文字或代码块标记。每个元素格式：\n"
                + "{\"type\":\"character|location|item|force|species|concept|event\",\"title\":\"名称\",\"subType\":\"子类型或空\",\"description\":\"一段简要描述\",\"attributes\":[{\"key\":\"属性名\",\"value\":\"属性值\"}],\"tags\":[\"标签\"]}\n\n"
                + "设定文字：\n" + dto.Text;

            try
            {
                var result = await CallAiAsync(baseUrl, apiKey, model, prompt);
                var cards = ExtractJsonArray<ImportCardItemDto>(result);
                if (cards.Count == 0) return BadRequest(new { message = "没能从文字里解析出卡片，请检查内容或换更清晰的设定文字" });
                return Ok(new { cards });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "AI 解析失败：" + ex.Message });
            }
        }

        // ===== AI 导入设定：确认批量创建卡片 =====
        [HttpPost("import-world/confirm")]
        public async Task<IActionResult> ConfirmImport([FromBody] ConfirmImportDto dto)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            if (!await _context.WorldProjects.AsNoTracking().AnyAsync(p => p.Id == dto.ProjectId && p.OwnerId == CurrentUserId.Value))
                return NotFound(new { message = "只能导入到自己的项目" });
            if (dto.Cards == null || dto.Cards.Count == 0) return BadRequest(new { message = "没有要创建的卡片" });

            var created = 0;
            var errors = new List<string>();
            foreach (var c in dto.Cards)
            {
                if (string.IsNullOrWhiteSpace(c.Title)) continue;
                try
                {
                    await _worldCardService.CreateCardAsync(dto.ProjectId, CurrentUserId.Value, new CreateCardDto
                    {
                        Title = c.Title.Trim(),
                        Type = string.IsNullOrWhiteSpace(c.Type) ? "concept" : c.Type,
                        SubType = c.SubType,
                        Description = c.Description,
                        Attributes = c.Attributes ?? new List<AttributeDto>(),
                        Tags = c.Tags ?? new List<string>()
                    });
                    created++;
                }
                catch (Exception ex)
                {
                    errors.Add($"「{c.Title}」：{ex.Message}");
                    if (errors.Count >= 5) break;
                }
            }
            return Ok(new { created, errors });
        }

        private async Task<(string apiKey, string baseUrl, string model, string? err)> GetAiCredentialsAsync(Guid userId)
        {
            var cfg = await _context.UserAiConfigs.FirstOrDefaultAsync(c => c.UserId == userId);
            if (cfg == null || string.IsNullOrEmpty(cfg.EncryptedApiKey))
                return (string.Empty, string.Empty, string.Empty, "请先在个人中心配置你的 AI（接口地址、Key、模型）");
            var apiKey = AiKeyCrypto.Decrypt(cfg.EncryptedApiKey, EncryptionSecret);
            if (string.IsNullOrEmpty(apiKey)) return (string.Empty, string.Empty, string.Empty, "AI Key 解密失败，请重新配置");
            return (apiKey, cfg.BaseUrl, cfg.Model, null);
        }

        private static List<T> ExtractJsonArray<T>(string text)
        {
            var list = new List<T>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            var s = text.Trim();
            var start = s.IndexOf('[');
            var end = s.LastIndexOf(']');
            if (start < 0 || end <= start) return list;
            var json = s.Substring(start, end - start + 1);
            try
            {
                var items = JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null) list.AddRange(items);
            }
            catch { /* 解析失败返回空 */ }
            return list;
        }

        private static string BuildPrompt(IEnumerable<dynamic> cards)
        {
            var sb = new StringBuilder();
            sb.AppendLine("以下是一个世界观项目的设定内容，请帮我做一次完整整理：");
            sb.AppendLine();
            foreach (var c in cards)
            {
                var type = c.Type ?? "未分类";
                if (!string.IsNullOrWhiteSpace(c.SubType)) type += "/" + c.SubType;
                sb.AppendLine($"【{type}】{c.Title}");
                if (!string.IsNullOrWhiteSpace(c.Description)) sb.AppendLine($"描述：{c.Description}");
                if (!string.IsNullOrWhiteSpace(c.Attributes) && c.Attributes != "[]") sb.AppendLine($"属性：{c.Attributes}");
                if (!string.IsNullOrWhiteSpace(c.Tags) && c.Tags != "[]") sb.AppendLine($"标签：{c.Tags}");
                sb.AppendLine();
            }
            sb.AppendLine("请输出一份「世界观整理报告」，用 Markdown 格式，包含：");
            sb.AppendLine("## 一、世界观脉络梳理");
            sb.AppendLine("## 二、人物 / 角色关系分析");
            sb.AppendLine("## 三、设定矛盾与冲突点");
            sb.AppendLine("## 四、可补充的设定建议");
            return sb.ToString();
        }

        private async Task<string> CallAiAsync(string baseUrl, string apiKey, string model, string prompt)
        {
            var url = baseUrl.TrimEnd('/') + "/chat/completions";
            var payload = new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = "你是一位专业的世界观设定整理助手，擅长梳理设定脉络、分析人物关系、发现设定矛盾并给出补充建议。" },
                    new { role = "user", content = prompt }
                },
                temperature = 0.7
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var client = _httpFactory.CreateClient();
            var resp = await client.SendAsync(req, cts.Token);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return $"AI 调用失败（{(int)resp.StatusCode}）：{Truncate(body, 300)}";

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return content ?? "（无返回内容）";
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s[..max] + "…";
        }
    }
}
