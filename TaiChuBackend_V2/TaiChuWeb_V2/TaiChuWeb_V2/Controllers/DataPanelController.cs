using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext; //

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataPanelController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DataPanelController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
        {
            try
            {
                // 首页给所有人看的数字：只数大家能看到的（和画廊、论坛、百科页面上看到的一致），
                // 下架 / 审核中 / 没通过的画作、撤回或设为私密的帖子博客、删除的词条都不算。
                // 管理面板「数据」里有包含全部内容的统计（/api/Admin/Analytics/content）。
                var userCount = await _context.Users.CountAsync();
                var workCount = await _context.Artworks.CountAsync(a => a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null));
                var visibleNotes = TaiChuWeb_V2.Controllers.LingMai.LingMaiPublishController.VisiblePublishedNotes(_context);
                var blogCount = await visibleNotes.CountAsync(n => n.Type == "blog");
                var postCount = await visibleNotes.CountAsync(n => n.Type == "post");
                var projectCount = await _context.Projects.CountAsync();
                var wikiCount = await _context.WikiArticles.CountAsync(a => !a.IsDeleted && a.CurrentRevisionId != null);

                var stats = new StatsOverviewDto
                {
                    UserCount = userCount,
                    WorkCount = workCount,
                    BlogCount = blogCount,
                    PostCount = postCount,
                    WikiCount = wikiCount,
                    ProjectCount = projectCount // 🌟 新增项目计数
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "跨表清算太初寰宇综合灵脉指标失败", error = ex.Message });
            }
        }
    }

    /// <summary>
    /// 全面扩容版寰宇指标传输对象 (DTO)
    /// </summary>
    public class StatsOverviewDto
    {
        public int UserCount { get; set; }
        public int WorkCount { get; set; }
        public int BlogCount { get; set; }
        public int PostCount { get; set; }
        public int WikiCount { get; set; }

        public int ProjectCount { get; set; }
    }
}