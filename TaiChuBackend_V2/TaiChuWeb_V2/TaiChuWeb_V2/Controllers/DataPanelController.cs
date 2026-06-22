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
                // 1. 基础物理表清算：行者人数与艺术作品数
                var userCount = await _context.Users.CountAsync(); //
                var workCount = await _context.Artworks.CountAsync(); //

                // 2. 灵脉发布区清算：方志(博客)与心声(帖子)均在 PublishedNotes 表中
                // 严格匹配你指定的区分逻辑：Type 为 "blog" 算作博客，Type 为 "post" 算作帖子
                var blogCount = await _context.PublishedNotes.CountAsync(n => n.Type == "blog");
                var postCount = await _context.PublishedNotes.CountAsync(n => n.Type == "post");
                var projectCount = await _context.Projects.CountAsync(); // 统计所有项目总数
                // 3. 百科知识库清算：统计 wiki_articles 表中未被软删除的有效文章总数
                var wikiCount = await _context.WikiArticles.CountAsync(a => !a.IsDeleted); //

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