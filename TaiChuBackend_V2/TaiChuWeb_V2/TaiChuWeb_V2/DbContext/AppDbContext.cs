using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.Models.Artwork;
using TaiChuWeb_V2.Models.Interact;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.Plugin;
using TaiChuWeb_V2.Models.Project;
using TaiChuWeb_V2.Models.Tag;
using TaiChuWeb_V2.Models.Trade;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Models.Wiki;
using TaiChuWeb_V2.Models.Event;
using TaiChuWeb_V2.Models.Feedback;
using TaiChuWeb_V2.Models.News;
using TaiChuWeb_V2.Models.Financial;
using TaiChuWeb_V2.Models.Activity;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Models.Survey;



namespace TaiChuWeb_V2.DbContext
{
    public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }



        // ===== 问卷系统 =====
        public DbSet<Survey> Surveys { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<SurveySubmission> SurveySubmissions { get; set; }
        public DbSet<Answer> Answers { get; set; }



        public DbSet<Event> Events { get; set; }

        public DbSet<Financial> Financials { get; set; }
        public DbSet<News> News { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<TagAssignment> TagAssignments { get; set; }
        public DbSet<UserInteraction> UserInteractions { get; set; }

        public DbSet<EmailVerification> EmailVerifications { get; set; }
        public DbSet<Plugin> Plugins { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserStats> UserStats { get; set; }
        public DbSet<UserSignLog> UserSignLogs { get; set; }

        public DbSet<Artwork> Artworks { get; set; }
        public DbSet<ArtworkImage> ArtworkImages { get; set; }

        // --- 【灵脉 2.0 核心 DbSet】 ---
        public DbSet<Note> Notes { get; set; }
        public DbSet<Block> Blocks { get; set; }
        public DbSet<Space> Spaces { get; set; }
        public DbSet<NoteLink> NoteLinks { get; set; }
        public DbSet<NoteHistory> NoteHistories { get; set; }
        public DbSet<Comment> Comments { get; set; }

        public DbSet<PublishedNote> PublishedNotes { get; set; }
        public DbSet<PublishedBlock> PublishedBlocks { get; set; }

        // --- 🌟 2. 添加 Wiki 的元数据 DbSet ---
        public DbSet<WikiCategory> WikiCategories { get; set; }
        public DbSet<WikiArticle> WikiArticles { get; set; }
        public DbSet<WikiArticleRevision> WikiArticleRevisions { get; set; }
        public DbSet<WikiCategoryRequest> WikiCategoryRequests { get; set; }

        // --- 【交易系统核心 DbSet】 ---
        public DbSet<StoreItem> StoreItems { get; set; }
        public DbSet<UserPurchaseProgress> UserPurchaseProgress { get; set; }

        public DbSet<StoreItemSecret> StoreItemSecrets { get; set; }


        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectMember> ProjectMembers { get; set; }
        public DbSet<ProjectTask> ProjectTasks { get; set; }
        public DbSet<ProjectCategory> ProjectCategories { get; set; }
        public DbSet<ProjectDocument> ProjectDocuments { get; set; }
        public DbSet<ProjectApplication> ProjectApplications { get; set; }


        public DbSet<Activity> Activities { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<Record> Records { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Reply> Replies { get; set; }

        public DbSet<WorldProject> WorldProjects { get; set; }
        public DbSet<WorldCard> WorldCards { get; set; }
        public DbSet<WorldRelation> WorldRelations { get; set; }

        public DbSet<CardType> CardTypes { get; set; }








        public DbSet<ActivityType> ActivityTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            // 👇 世界模块配置
            modelBuilder.Entity<WorldProject>()
                .HasIndex(p => p.OwnerId);

            modelBuilder.Entity<WorldCard>()
                .HasIndex(c => c.ProjectId);

            modelBuilder.Entity<WorldCard>()
                .HasIndex(c => c.Type);

            modelBuilder.Entity<WorldRelation>()
                .HasIndex(r => r.SourceCardId);

            modelBuilder.Entity<WorldRelation>()
                .HasIndex(r => r.TargetCardId);

            // JSON 字段默认值
            modelBuilder.Entity<WorldCard>()
                .Property(c => c.Aliases)
                .HasDefaultValue("[]");

            modelBuilder.Entity<WorldCard>()
                .Property(c => c.Attributes)
                .HasDefaultValue("[]");

            modelBuilder.Entity<WorldCard>()
                .Property(c => c.ContentBlocks)
                .HasDefaultValue("[]");

            modelBuilder.Entity<WorldCard>()
                .Property(c => c.TimelineEvents)
                .HasDefaultValue("[]");

            modelBuilder.Entity<WorldCard>()
                .Property(c => c.Tags)
                .HasDefaultValue("[]");

            modelBuilder.Entity<WorldCard>()
                .Property(c => c.EmbeddedCards)
                .HasDefaultValue("[]");


            modelBuilder.Entity<WorldCard>()
.HasMany(c => c.OutRelations)
.WithOne(r => r.SourceCard)
.HasForeignKey(r => r.SourceCardId)
.OnDelete(DeleteBehavior.Restrict);

            // 2. InRelations：WorldCard 作为目标（TargetCard）
            modelBuilder.Entity<WorldCard>()
                .HasMany(c => c.InRelations)
                .WithOne(r => r.TargetCard)
                .HasForeignKey(r => r.TargetCardId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CardType>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.HasIndex(t => t.IsActive);
                entity.HasIndex(t => t.SortOrder);
            });

            // ===== 卡片类型种子数据 =====
            modelBuilder.Entity<CardType>().HasData(
                new CardType { Id = "character", Label = "角色", Icon = "🧙", SortOrder = 1, IsSystem = true, CreatedAt = DateTime.UtcNow },
                new CardType { Id = "location", Label = "地点", Icon = "📍", SortOrder = 2, IsSystem = true, CreatedAt = DateTime.UtcNow },
                new CardType { Id = "item", Label = "物品", Icon = "⚔️", SortOrder = 3, IsSystem = true, CreatedAt = DateTime.UtcNow },
                new CardType { Id = "event", Label = "事件", Icon = "📖", SortOrder = 4, IsSystem = true, CreatedAt = DateTime.UtcNow },
                new CardType { Id = "ecology", Label = "生态", Icon = "🌿", SortOrder = 5, IsSystem = true, CreatedAt = DateTime.UtcNow },
                new CardType { Id = "faction", Label = "派系", Icon = "🏛️", SortOrder = 6, IsSystem = true, CreatedAt = DateTime.UtcNow },
                new CardType { Id = "species", Label = "物种", Icon = "🐉", SortOrder = 7, IsSystem = true, CreatedAt = DateTime.UtcNow },
                new CardType { Id = "lore", Label = "背景设定", Icon = "📜", SortOrder = 8, IsSystem = true, CreatedAt = DateTime.UtcNow }
            );





            modelBuilder.Entity<ProjectMember>()
            .HasKey(pm => new { pm.ProjectId, pm.UserId }); // 联合主键

            modelBuilder.Entity<ProjectDocument>()
                .HasKey(pd => new { pd.ProjectId, pd.NoteId }); // 联合主键

            // 可选：设置级联删除，项目删除时，自动清理底下的任务和分类
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Tasks)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.Categories)
                .WithOne(c => c.Project)
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            // --- 🌟【交易系统模型配置】 ---

            // 1. StoreItem 配置
            modelBuilder.Entity<StoreItem>(entity =>
            {
                entity.ToTable("store_items");

                // 索引优化：频繁按类别和激活状态筛选
                entity.HasIndex(i => new { i.Category, i.IsActive });
                entity.HasIndex(i => i.SortOrder);
            });

            // 2. UserPurchaseProgress 配置（核心：复合唯一索引）
            // 🌟 核心配置：定义复合主键 (UserId + StoreItemId)
            // 根据你的模型，一个用户针对一个特定的商品只能有一行进度数据
            modelBuilder.Entity<UserPurchaseProgress>()
                .HasKey(p => new { p.UserId, p.StoreItemId });

            // 配置关联关系
            modelBuilder.Entity<UserPurchaseProgress>()
                .HasOne(p => p.Item)
                .WithMany()
                .HasForeignKey(p => p.StoreItemId);
            modelBuilder.Entity<UserPermission>(entity =>
            {
                entity.ToTable("user_permissions");
                // 复合索引：加速权限校验
                entity.HasIndex(p => new { p.UserId, p.Permission }).IsUnique();
            });

            // 1. Tag 的 NormalizedName 保持唯一
            modelBuilder.Entity<Tag>()
                .HasIndex(t => t.NormalizedName)
                .IsUnique();

            // 2. 为 TagAssignment 建立复合唯一索引
            modelBuilder.Entity<TagAssignment>(entity =>
            {
                entity.ToTable("tag_assignments");
                entity.HasIndex(ta => new { ta.EntityType, ta.EntityId, ta.TagId }).IsUnique();
                entity.HasIndex(ta => ta.TagId);
            });

            modelBuilder.Entity<NoteLink>(entity =>
            {
                entity.HasKey(e => e.Id);

                // 源笔记配置
                entity.HasOne(d => d.SourceNote)
                    .WithMany()
                    .HasForeignKey(d => d.SourceNoteId)
                    .OnDelete(DeleteBehavior.Cascade);

                // 目标笔记配置
                entity.HasOne(d => d.TargetNote)
                    .WithMany()
                    .HasForeignKey(d => d.TargetNoteId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Plugin>()
                .Property(p => p.PlatformScope)
                .HasDefaultValue(0);

            // 用户表唯一性索引配置
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<User>()
                .HasOne(u => u.Profile).WithOne(p => p.User).HasForeignKey<UserProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Stats).WithOne(s => s.User).HasForeignKey<UserStats>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSignLog>()
                .HasOne(l => l.User).WithMany(u => u.SignLogs).HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSignLog>()
                .HasIndex(l => new { l.UserId, l.SignDate }).IsUnique();

            // 配置 Artwork 关系
            modelBuilder.Entity<Artwork>()
                .HasOne(a => a.Uploader).WithMany().HasForeignKey(a => a.UploaderId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ArtworkImage>()
                .HasOne(ai => ai.Artwork).WithMany(a => a.Images).HasForeignKey(ai => ai.ArtworkId).OnDelete(DeleteBehavior.Cascade);

            // --- 🌟【灵脉 2.0 优化多态重构】---

            // 1. 配置 Note 表的索引与多态投影
            modelBuilder.Entity<Note>(entity =>
            {
                entity.ToTable("notes");
                entity.HasIndex(n => n.SpaceId);
                entity.HasIndex(n => n.IsPublic);

                entity.HasOne<Artwork>()
                    .WithMany()
                    .HasForeignKey(n => n.TargetId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(n => new { n.IsPublic, n.Status, n.Type, n.CreatedAt });
            });

            // 2. 配置草稿区多态 Block 表（无级联外键，使用高速复合索引）
            modelBuilder.Entity<Block>(entity =>
            {
                entity.ToTable("blocks");

                entity.HasIndex(b => new { b.OwnerId, b.OwnerType })
                    .HasDatabaseName("IX_blocks_Owner");

                entity.HasIndex(b => new { b.OwnerId, b.OwnerType, b.SortOrder })
                    .HasDatabaseName("IX_blocks_Owner_SortOrder");

                entity.Property(b => b.Data)
                    .HasColumnType("json");
            });

            // 3. 配置发布区多态 PublishedBlock 表
            modelBuilder.Entity<PublishedBlock>(entity =>
            {
                entity.ToTable("PublishedBlocks");

                entity.HasIndex(pb => new { pb.OwnerId, pb.OwnerType })
                    .HasDatabaseName("IX_pub_blocks_Owner");

                entity.HasIndex(pb => new { pb.OwnerId, pb.OwnerType, pb.SortOrder })
                    .HasDatabaseName("IX_pub_blocks_Owner_SortOrder");
            });

            // 4. 配置 Comment 表
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.ToTable("comments");

                entity.HasOne(c => c.Parent)
                    .WithMany(c => c.Replies)
                    .HasForeignKey(c => c.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Note>()
                    .WithMany()
                    .HasForeignKey(c => c.NoteId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<Artwork>()
                    .WithMany()
                    .HasForeignKey(c => c.ArtworkId)
                    .OnDelete(DeleteBehavior.Cascade);
            });



            // ---- 活动模块配置 ----
            // 1. Member 复合唯一约束 (一个用户在一个活动中只能有一条记录)
            modelBuilder.Entity<Member>()
                .HasIndex(m => new { m.ActivityId, m.UserId })
                .IsUnique();

            // 2. Record 复合唯一约束 (一个成员同一天只能有一条记录)
            modelBuilder.Entity<Record>()
                .HasIndex(r => new { r.MemberId, r.Day })
                .IsUnique();

            // 3. 级联删除：活动删除时自动删除成员和帖子
            modelBuilder.Entity<Member>()
                .HasOne(m => m.Activity)
                .WithMany(a => a.Members)
                .HasForeignKey(m => m.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Post>()
                .HasOne(p => p.Activity)
                .WithMany(a => a.Posts)
                .HasForeignKey(p => p.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);

            // 4. Record 级联删除 (成员删除时自动删除记录)
            modelBuilder.Entity<Record>()
                .HasOne(r => r.Member)
                .WithMany()
                .HasForeignKey(r => r.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            // 5. Reply 级联删除 (帖子删除时自动删除回复)
            modelBuilder.Entity<Reply>()
                .HasOne(r => r.Post)
                .WithMany(p => p.Replies)
                .HasForeignKey(r => r.PostId)
                .OnDelete(DeleteBehavior.Cascade);



            // ===== 问卷系统配置 =====

            // Survey 索引
            modelBuilder.Entity<Survey>(entity =>
            {
                entity.ToTable("surveys");
                entity.HasIndex(s => s.Status);
                entity.HasIndex(s => new { s.StartTime, s.EndTime });
                entity.HasIndex(s => s.CreatedBy);
            });

            // Question 索引
            modelBuilder.Entity<Question>(entity =>
            {
                entity.ToTable("questions");
                entity.HasIndex(q => q.SurveyId);
                entity.HasIndex(q => new { q.SurveyId, q.SortOrder });
            });

            // QuestionOption 索引
            modelBuilder.Entity<QuestionOption>(entity =>
            {
                entity.ToTable("question_options");
                entity.HasIndex(o => o.QuestionId);
                entity.HasIndex(o => new { o.QuestionId, o.SortOrder });
            });

            // SurveySubmission 索引和唯一约束
            modelBuilder.Entity<SurveySubmission>(entity =>
            {
                entity.ToTable("survey_submissions");
                entity.HasIndex(s => s.SurveyId);
                entity.HasIndex(s => s.UserId);
                entity.HasIndex(s => s.SubmittedAt);

                // 同一用户对同一问卷只能提交一次（匿名用户通过 UserId=NULL + Identifier 控制）
                entity.HasIndex(s => new { s.SurveyId, s.UserId })
                    .IsUnique()
                    .HasDatabaseName("IX_SurveySubmission_Survey_User");
            });

            // Answer 索引
            modelBuilder.Entity<Answer>(entity =>
            {
                entity.ToTable("answers");
                entity.HasIndex(a => a.SubmissionId);
                entity.HasIndex(a => a.QuestionId);
            });

            // ===== 级联删除设置 =====
            // Survey → Question：删除问卷时级联删除题目
            modelBuilder.Entity<Question>()
                .HasOne(q => q.Survey)
                .WithMany(s => s.Questions)
                .HasForeignKey(q => q.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Question → QuestionOption：删除题目时级联删除选项
            modelBuilder.Entity<QuestionOption>()
                .HasOne(o => o.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Survey → SurveySubmission：删除问卷时级联删除提交记录
            modelBuilder.Entity<SurveySubmission>()
                .HasOne(s => s.Survey)
                .WithMany(s => s.Submissions)
                .HasForeignKey(s => s.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);

            // SurveySubmission → Answer：删除提交记录时级联删除答案
            modelBuilder.Entity<Answer>()
                .HasOne(a => a.Submission)
                .WithMany(s => s.Answers)
                .HasForeignKey(a => a.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);




        }
    }
}