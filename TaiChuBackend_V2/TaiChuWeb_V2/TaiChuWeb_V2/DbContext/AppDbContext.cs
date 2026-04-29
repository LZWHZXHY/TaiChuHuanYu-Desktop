using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.Models.Artwork;
using TaiChuWeb_V2.Models.Plugin;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Models.Interact;

namespace TaiChuWeb_V2.DbContext
{
    public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<UserInteraction> UserInteractions { get; set; }

        public DbSet<EmailVerification> EmailVerifications { get; set; }
        public DbSet<Plugin> Plugins { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserStats> UserStats { get; set; }

        public DbSet<UserSignLog> UserSignLogs { get; set; }


        public DbSet<Artwork> Artworks { get; set; }
        public DbSet<ArtworkImage> ArtworkImages { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Plugin>()
                .Property(p => p.PlatformScope)
                .HasDefaultValue(0); // 数据库层面的默认值



            // 1. 用户表唯一性索引配置
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
            });

            // 2. 配置 User 与 UserProfile 的 1:1 关系
            modelBuilder.Entity<User>()
                .HasOne(u => u.Profile)
                .WithOne(p => p.User)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 3. 配置 User 与 UserStats 的 1:1 关系
            modelBuilder.Entity<User>()
                .HasOne(u => u.Stats)
                .WithOne(s => s.User)
                .HasForeignKey<UserStats>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- 【核心新增：签到逻辑配置】 ---

            // 4. 配置 User 与 UserSignLog 的 1:N 关系
            modelBuilder.Entity<UserSignLog>()
                .HasOne(l => l.User)
                .WithMany(u => u.SignLogs)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 5. 【极其重要】建立 [UserId + SignDate] 的唯一索引
            // 物理层面保证一个用户在同一天（日期部分）只能有一条记录，防止并发 Bug
            modelBuilder.Entity<UserSignLog>()
                .HasIndex(l => new { l.UserId, l.SignDate })
                .IsUnique();

            // 6. 配置 Artwork 与 User 的 1:N 关系 (上传者)
            modelBuilder.Entity<Artwork>()
                .HasOne(a => a.Uploader)
                .WithMany() // 如果你在 User 类里没写 ICollection<Artwork>，这里留空
                .HasForeignKey(a => a.UploaderId)
                .OnDelete(DeleteBehavior.Cascade); // 用户注销时，其作品通常也级联删除

            // 7. 配置 Artwork 与 ArtworkImage 的 1:N 关系
            modelBuilder.Entity<ArtworkImage>()
                .HasOne(ai => ai.Artwork)
                .WithMany(a => a.Images)
                .HasForeignKey(ai => ai.ArtworkId)
                .OnDelete(DeleteBehavior.Cascade); // 作品删除时，自动清理关联图片记录

        }
    }
}