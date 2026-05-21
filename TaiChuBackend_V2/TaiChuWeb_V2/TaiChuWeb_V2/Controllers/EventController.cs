using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Event;
using TaiChuWeb_V2.Models.Event;

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EventController(AppDbContext context)
        {
            _context = context;
        }

        // ==============================================
        // 1. 获取当月活动 (前端日历使用) - 保持原有逻辑
        // ==============================================
        [HttpGet("month")]
        public async Task<IActionResult> GetMonthEvents([FromQuery] int year, [FromQuery] int month)
        {
            if (year < 2000 || month < 1 || month > 12)
            {
                return BadRequest(new { message = "无效的年月参数" });
            }

            var queryMonthStart = new DateTime(year, month, 1);
            var queryMonthEnd = queryMonthStart.AddMonths(1).AddDays(-1);

            var events = await _context.Events
                .Where(e => e.Status != EventStatus.Draft &&
                            e.StartDate <= queryMonthEnd &&
                            e.EndDate >= queryMonthStart)
                .ToListAsync();

            var dailyEventsDict = new Dictionary<string, List<EventDto>>();

            foreach (var evt in events)
            {
                var dto = new EventDto
                {
                    Id = evt.Id,
                    Title = evt.Title,
                    Description = evt.Description,
                    StartDate = evt.StartDate.ToString("yyyy-MM-dd"),
                    EndDate = evt.EndDate.ToString("yyyy-MM-dd"),
                    StartTime = evt.StartTime,
                    EndTime = evt.EndTime,
                    Status = (int)evt.Status
                };

                var displayStart = evt.StartDate < queryMonthStart ? queryMonthStart : evt.StartDate;
                var displayEnd = evt.EndDate > queryMonthEnd ? queryMonthEnd : evt.EndDate;

                for (var date = displayStart.Date; date <= displayEnd.Date; date = date.AddDays(1))
                {
                    var dateKey = date.ToString("yyyy-MM-dd");
                    if (!dailyEventsDict.ContainsKey(dateKey))
                    {
                        dailyEventsDict[dateKey] = new List<EventDto>();
                    }
                    dailyEventsDict[dateKey].Add(dto);
                }
            }

            foreach (var key in dailyEventsDict.Keys)
            {
                dailyEventsDict[key] = dailyEventsDict[key]
                    .OrderBy(e => e.StartTime ?? "23:59")
                    .ToList();
            }

            return Ok(new { code = 200, data = dailyEventsDict, message = "success" });
        }


        // ==============================================
        // 2. 获取所有活动 (管理后台使用)
        // ==============================================
        [HttpGet]
        public async Task<IActionResult> GetAllEvents()
        {
            var events = await _context.Events
                .OrderByDescending(e => e.StartDate) // 后台默认按开始时间倒序排列
                .ToListAsync();

            var dtos = events.Select(evt => new EventDto
            {
                Id = evt.Id,
                Title = evt.Title,
                Description = evt.Description,
                StartDate = evt.StartDate.ToString("yyyy-MM-dd"),
                EndDate = evt.EndDate.ToString("yyyy-MM-dd"),
                StartTime = evt.StartTime,
                EndTime = evt.EndTime,
                Status = (int)evt.Status
            }).ToList();

            return Ok(new { code = 200, data = dtos, message = "success" });
        }


        // ==============================================
        // 3. 创建新活动
        // ==============================================
        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] EventDto dto)
        {
            // 解析前端传来的 YYYY-MM-DD
            if (!DateTime.TryParse(dto.StartDate, out var startDate) ||
                !DateTime.TryParse(dto.EndDate, out var endDate))
            {
                return BadRequest(new { code = 400, message = "日期格式不正确" });
            }

            var newEvent = new Event
            {
                Title = dto.Title,
                Description = dto.Description,
                StartDate = startDate,
                EndDate = endDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Status = (EventStatus)dto.Status,
                CreatedAt = DateTime.UtcNow
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = newEvent.Id, message = "创建成功" });
        }


        // ==============================================
        // 4. 更新活动完整信息
        // ==============================================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvent(string id, [FromBody] EventDto dto)
        {
            var existingEvent = await _context.Events.FindAsync(id);
            if (existingEvent == null) return NotFound(new { code = 404, message = "未找到该活动" });

            if (!DateTime.TryParse(dto.StartDate, out var startDate) ||
                !DateTime.TryParse(dto.EndDate, out var endDate))
            {
                return BadRequest(new { code = 400, message = "日期格式不正确" });
            }

            existingEvent.Title = dto.Title;
            existingEvent.Description = dto.Description;
            existingEvent.StartDate = startDate;
            existingEvent.EndDate = endDate;
            existingEvent.StartTime = dto.StartTime;
            existingEvent.EndTime = dto.EndTime;
            existingEvent.Status = (EventStatus)dto.Status;

            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = existingEvent.Id, message = "更新成功" });
        }


        // ==============================================
        // 5. 快捷更新活动状态
        // ==============================================
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateEventStatus(string id, [FromBody] UpdateStatusRequest req)
        {
            var existingEvent = await _context.Events.FindAsync(id);
            if (existingEvent == null) return NotFound(new { code = 404, message = "未找到该活动" });

            existingEvent.Status = (EventStatus)req.Status;
            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = existingEvent.Id, message = "状态更新成功" });
        }


        // ==============================================
        // 6. 删除活动
        // ==============================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(string id)
        {
            var existingEvent = await _context.Events.FindAsync(id);
            if (existingEvent == null) return NotFound(new { code = 404, message = "未找到该活动" });

            _context.Events.Remove(existingEvent);
            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = id, message = "删除成功" });
        }
    }

    /// <summary>
    /// 用于接收局部状态更新的实体类
    /// </summary>
    public class UpdateStatusRequest
    {
        public int Status { get; set; }
    }
}