namespace TaiChuWeb_V2.Models.User
{
    public class UserPermission
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public AdminPermission Permission { get; set; }

        // 导航属性
        public virtual User User { get; set; } = null!;
    }
}
