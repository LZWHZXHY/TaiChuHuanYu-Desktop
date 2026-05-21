using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Event
{
    [Table("Events")]
    public class Event
    {
        [Key]
        [Column(TypeName = "varchar(36)")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;     // 活动的标题

        public string? Description { get; set; }              // 一个描述

        [Required]
        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }               // 开始日期

        [Required]
        [Column(TypeName = "date")]
        public DateTime EndDate { get; set; }                 // 结束日期

        [MaxLength(10)]
        public string? StartTime { get; set; }                // 开始时间 (例如 "09:00")

        [MaxLength(10)]
        public string? EndTime { get; set; }                  // 结束时间 (例如 "18:00")

        public EventStatus Status { get; set; } = EventStatus.Published; // 活动状态

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum EventStatus
    {
        Draft = 0,       // 草稿/未发布
        Published = 1,   // 已发布/未开始
        Ongoing = 2,     // 进行中
        Completed = 3,   // 已结束
        Cancelled = 4    // 已取消
    }
}