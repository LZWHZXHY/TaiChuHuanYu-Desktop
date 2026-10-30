using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Services.Chat;

namespace TaiChuWeb_V2.Controllers.Admin
{
    public sealed class SaveChatChannelDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool AllowAi { get; set; } = true;
        /// <summary>all 或 ai-only</summary>
        public string PostPolicy { get; set; } = "all";
        public bool Research { get; set; }
    }

    public sealed class SetBotEnabledDto
    {
        public bool Enabled { get; set; }
    }

    /// <summary>管理后台 · 聊天室频道管理（权限：User_Audit，由 AdminAccessFilter 校验）。</summary>
    [Authorize]
    [ApiController]
    [Route("api/Admin/Chat")]
    public partial class AdminChatController(ChatStore store, BotGateway gateway) : ControllerBase
    {
        [GeneratedRegex("^[a-z0-9-]{2,32}$")]
        private static partial Regex IdPattern();

        [HttpGet("channels")]
        public async Task<IActionResult> Channels() => Ok(await store.ChannelsAsync());

        [HttpPut("channels")]
        public async Task<IActionResult> Save([FromBody] SaveChatChannelDto dto)
        {
            var id = (dto.Id ?? string.Empty).Trim().ToLowerInvariant();
            if (!IdPattern().IsMatch(id)) return BadRequest(new { message = "频道编号只能用 2–32 位小写字母、数字和短横线，例如 mc-build" });
            var name = (dto.Name ?? string.Empty).Trim();
            if (name.Length is < 1 or > 20) return BadRequest(new { message = "频道名称需要 1–20 个字" });
            var description = (dto.Description ?? string.Empty).Trim();
            var icon = (dto.Icon ?? string.Empty).Trim();
            await store.SaveChannelAsync(new ChatChannel
            {
                Id = id,
                Name = name,
                Description = description.Length > 200 ? description[..200] : description,
                Icon = icon.Length == 0 ? "#" : icon.Length > 8 ? icon[..8] : icon,
                SortOrder = dto.SortOrder,
                AllowAi = dto.AllowAi,
                PostPolicy = dto.PostPolicy == "ai-only" ? "ai-only" : "all",
                Research = dto.Research,
            });
            gateway.InvalidateChannels();
            return Ok(new { message = "已保存" });
        }

        [HttpDelete("channels/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var channels = await store.ChannelsAsync();
            if (channels.Count <= 1) return BadRequest(new { message = "至少保留一个频道" });
            await store.DeleteChannelAsync(id);
            gateway.InvalidateChannels();
            return NoContent();
        }

        /// <summary>所有接入的机器人（管理员可一键停用刷屏或违规的机器人）</summary>
        [HttpGet("bots")]
        public async Task<IActionResult> Bots()
        {
            var bots = await store.BotsAsync();
            return Ok(bots.Select(b => new { b.Id, b.Name, b.OwnerId, b.Description, b.Enabled, b.Channels, b.CreatedAt, b.LastSeenAt, online = gateway.IsOnline(b.Id) }));
        }

        [HttpPut("bots/{id}/enabled")]
        public async Task<IActionResult> SetBotEnabled(string id, [FromBody] SetBotEnabledDto dto)
        {
            var bot = await store.BotAsync(id);
            if (bot == null) return NotFound();
            bot.Enabled = dto.Enabled;
            await store.SaveBotAsync(bot);
            await gateway.RefreshBotAsync(id, disconnect: !dto.Enabled);
            return Ok(new { message = dto.Enabled ? "已启用" : "已停用并断开连接" });
        }
    }
}
