// TaiChuWeb_V2/Dtos/LingMai/UpdatePublishStatusDto.cs
using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.LingMai
{
    public class UpdatePublishStatusDto
    {
        /// <summary>
        /// 🌟 是否发布至广场
        /// </summary>
        [Required]
        public bool IsPublic { get; set; }
    }
}