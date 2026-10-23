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

        // 农历新年（春节）日期表：生肖按农历新年分界，而非公历 1 月 1 日。
        private static readonly Dictionary<int, (int Month, int Day)> LunarNewYearDates = new()
        {
            { 1920, (2, 20) }, { 1921, (2, 8) }, { 1922, (1, 28) }, { 1923, (2, 16) }, { 1924, (2, 5) },
            { 1925, (1, 24) }, { 1926, (2, 13) }, { 1927, (2, 2) }, { 1928, (1, 23) }, { 1929, (2, 10) },
            { 1930, (1, 30) }, { 1931, (2, 17) }, { 1932, (2, 6) }, { 1933, (1, 26) }, { 1934, (2, 14) },
            { 1935, (2, 4) }, { 1936, (1, 24) }, { 1937, (2, 11) }, { 1938, (1, 31) }, { 1939, (2, 19) },
            { 1940, (2, 8) }, { 1941, (1, 27) }, { 1942, (2, 15) }, { 1943, (2, 5) }, { 1944, (1, 25) },
            { 1945, (2, 13) }, { 1946, (2, 2) }, { 1947, (1, 22) }, { 1948, (2, 10) }, { 1949, (1, 29) },
            { 1950, (2, 17) }, { 1951, (2, 6) }, { 1952, (1, 27) }, { 1953, (2, 14) }, { 1954, (2, 3) },
            { 1955, (1, 24) }, { 1956, (2, 12) }, { 1957, (1, 31) }, { 1958, (2, 18) }, { 1959, (2, 8) },
            { 1960, (1, 28) }, { 1961, (2, 15) }, { 1962, (2, 5) }, { 1963, (1, 25) }, { 1964, (2, 13) },
            { 1965, (2, 2) }, { 1966, (1, 21) }, { 1967, (2, 9) }, { 1968, (1, 30) }, { 1969, (2, 17) },
            { 1970, (2, 6) }, { 1971, (1, 27) }, { 1972, (2, 15) }, { 1973, (2, 3) }, { 1974, (1, 23) },
            { 1975, (2, 11) }, { 1976, (1, 31) }, { 1977, (2, 18) }, { 1978, (2, 7) }, { 1979, (1, 28) },
            { 1980, (2, 16) }, { 1981, (2, 5) }, { 1982, (1, 25) }, { 1983, (2, 13) }, { 1984, (2, 2) },
            { 1985, (2, 20) }, { 1986, (2, 9) }, { 1987, (1, 29) }, { 1988, (2, 17) }, { 1989, (2, 6) },
            { 1990, (1, 27) }, { 1991, (2, 15) }, { 1992, (2, 4) }, { 1993, (1, 23) }, { 1994, (2, 10) },
            { 1995, (1, 31) }, { 1996, (2, 19) }, { 1997, (2, 7) }, { 1998, (1, 28) }, { 1999, (2, 16) },
            { 2000, (2, 5) }, { 2001, (1, 24) }, { 2002, (2, 12) }, { 2003, (2, 1) }, { 2004, (1, 22) },
            { 2005, (2, 9) }, { 2006, (1, 29) }, { 2007, (2, 18) }, { 2008, (2, 7) }, { 2009, (1, 26) },
            { 2010, (2, 14) }, { 2011, (2, 3) }, { 2012, (1, 23) }, { 2013, (2, 10) }, { 2014, (1, 31) },
            { 2015, (2, 19) }, { 2016, (2, 8) }, { 2017, (1, 28) }, { 2018, (2, 16) }, { 2019, (2, 5) },
            { 2020, (1, 25) }, { 2021, (2, 12) }, { 2022, (2, 1) }, { 2023, (1, 22) }, { 2024, (2, 10) },
            { 2025, (1, 29) }, { 2026, (2, 17) }, { 2027, (2, 6) }, { 2028, (1, 26) }, { 2029, (2, 13) },
            { 2030, (2, 3) }, { 2031, (1, 23) }, { 2032, (2, 11) }, { 2033, (1, 31) }, { 2034, (2, 19) },
            { 2035, (2, 8) }, { 2036, (1, 28) }, { 2037, (2, 15) }, { 2038, (2, 4) }, { 2039, (1, 24) },
            { 2040, (2, 12) },
        };

        private string CalculateChineseZodiac(DateTime? birthday)
        {
            if (!birthday.HasValue) return "未知";

            // 十二生肖对照表
            string[] zodiacs = { "鼠", "牛", "虎", "兔", "龙", "蛇", "马", "羊", "猴", "鸡", "狗", "猪" };

            // 以 1900 年（鼠年）为基准；但生肖按农历新年分界，春节前的生日属上一年生肖
            int year = birthday.Value.Year;
            if (LunarNewYearDates.TryGetValue(year, out var lny) && birthday.Value < new DateTime(year, lny.Month, lny.Day))
                year -= 1;

            int index = ((year - 1900) % 12 + 12) % 12;
            return zodiacs[index];
        }


        public virtual User User { get; set; } = null!;
    }
}
