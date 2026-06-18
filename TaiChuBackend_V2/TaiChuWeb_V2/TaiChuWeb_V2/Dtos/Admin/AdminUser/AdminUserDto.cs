namespace TaiChuWeb_V2.Dtos.Admin.AdminUser
{
    public class AdminUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Permissions { get; set; } = new();
        public AdminUserProfileDto? Profile { get; set; }
        public AdminUserStatsDto? Stats { get; set; }
    }

    public class AdminUserProfileDto
    {
        public string? Avatar { get; set; }
        public string? Gender { get; set; }
        public string? Bio { get; set; }
        public string? Mood { get; set; }
        public string? Birthday { get; set; }
        public string? PhoneNumber { get; set; }
        public int Age { get; set; }
        public string Zodiac { get; set; } = string.Empty;
        public string ChineseZodiac { get; set; } = string.Empty;
    }

    public class AdminUserStatsDto
    {
        public int Level { get; set; }
        public long Experience { get; set; }
        public int Reputation { get; set; }
        public string? Title { get; set; }
        public int CurrentSignStreak { get; set; }
        public int MaxSignStreak { get; set; }
        public int UsedNotes { get; set; }
        public int UsedSpaces { get; set; }
        public int MaxNotes { get; set; }
        public int MaxSpaces { get; set; }
        public int MaxProjectCount { get; set; }
    }

    public class UpdateStatsPayload
    {
        public int Reputation { get; set; }
        public long Experience { get; set; }
        public int MaxSpaces { get; set; }
        public int MaxNotes { get; set; }
        public int MaxProjectCount { get; set; }
    }

    public class PunishPayload
    {
        public int Deduction { get; set; }
    }
}
