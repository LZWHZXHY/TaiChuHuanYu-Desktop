using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.User
{
    public class UserProfile
    {
        [Key]
        public Guid UserId { get; set; } // 既是主键也是外键

        [MaxLength(200)]
        public string? Avatar { get; set; } // 存储头像的 URL 路径

        // 修改：改为 string 类型，支持用户自定义输入
        [MaxLength(20)]
        public string? Gender { get; set; }

        // 新增：自我介绍
        [MaxLength(1000)]
        public string? Bio { get; set; }

        // 新增：当前心情/签名
        [MaxLength(50)]
        public string? Mood { get; set; }
        public DateTime? Birthday { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }

        [MaxLength(2000)]
        public string? SocialLinks { get; set; } // 存储 JSON 字符串，例如：[{"platform":"BiliBili", "url":"..."}, {"platform":"Xiaohongshu", "url":"..."}]

        [Column(TypeName = "text")]
        public string? ExtraConfig { get; set; } // 存储 JSON 配置信息

        // 自动计算属性（不需要存入数据库）
        [NotMapped]
        public string Zodiac => CalculateZodiac(Birthday);

        // --- 核心修复：添加星座计算逻辑 ---
        private string CalculateZodiac(DateTime? birthday)
        {
            if (!birthday.HasValue) return "未知";

            int month = birthday.Value.Month;
            int day = birthday.Value.Day;

            return month switch
            {
                1 => day <= 19 ? "摩羯座" : "水瓶座",
                2 => day <= 18 ? "水瓶座" : "双鱼座",
                3 => day <= 20 ? "双鱼座" : "白羊座",
                4 => day <= 19 ? "白羊座" : "金牛座",
                5 => day <= 20 ? "金牛座" : "双子座",
                6 => day <= 21 ? "双子座" : "巨蟹座",
                7 => day <= 22 ? "巨蟹座" : "狮子座",
                8 => day <= 22 ? "狮子座" : "处女座",
                9 => day <= 22 ? "处女座" : "天秤座",
                10 => day <= 23 ? "天秤座" : "天蝎座",
                11 => day <= 22 ? "天蝎座" : "射手座",
                12 => day <= 21 ? "射手座" : "摩羯座",
                _ => "未知"
            };
        }

        [NotMapped]
        public int Age => CalculateAge(Birthday);

        private int CalculateAge(DateTime? birthday)
        {
            if (!birthday.HasValue) return 0;

            var today = DateTime.Today;
            var age = today.Year - birthday.Value.Year;

            // 如果还没过今年的生日，年龄减一
            if (birthday.Value.Date > today.AddYears(-age)) age--;

            return age;
        }



        [NotMapped]
        public string ChineseZodiac => CalculateChineseZodiac(Birthday);

        private string CalculateChineseZodiac(DateTime? birthday)
        {
            if (!birthday.HasValue) return "未知";

            // 十二生肖对照表
            string[] zodiacs = { "鼠", "牛", "虎", "兔", "龙", "蛇", "马", "羊", "猴", "鸡", "狗", "猪" };

            // 以 1900 年（鼠年）为基准
            int offset = (birthday.Value.Year - 1900) % 12;
            return zodiacs[Math.Abs(offset)];
        }


        public virtual User User { get; set; } = null!;
    }
}
