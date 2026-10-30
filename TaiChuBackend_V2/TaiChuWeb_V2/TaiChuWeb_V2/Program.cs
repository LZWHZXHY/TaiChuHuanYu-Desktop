using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Filters;
using TaiChuWeb_V2.Hubs;
using TaiChuWeb_V2.Services;
using TaiChuWeb_V2.Services.Cos;
using Minio;
using TaiChuWeb_V2.Services.Email;
using TaiChuWeb_V2.Services.LingMai;
using TaiChuWeb_V2.Services.Project;
using TaiChuWeb_V2.Services.Publish;
using TaiChuWeb_V2.Services.Trade;
using TaiChuWeb_V2.Services.World;
using TaiChuWeb_V2.Services.Analytics;
using TaiChuWeb_V2.Services.Chat;
using TaiChuWeb_V2.Services.Project;
var builder = WebApplication.CreateBuilder(args);

// 1. 获取连接字符串
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. 数据库配置
var serverVersion = new MySqlServerVersion(new Version(8, 0, 31));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
        mySqlOptions.EnableRetryOnFailure(3)
    ));

// 3. 核心服务
builder.Services.AddOpenApi();

// 4. 配置跨域（为 SignalR 调整 CORS）
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // ⭐ 替代 AllowAnyOrigin，允许任意来源并支持凭据
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();          // ⭐ SignalR 携带 Token/凭据必需
    });
});

builder.Services.AddSignalR();
builder.Services.AddScoped<IWorldRelationService, WorldRelationService>();
builder.Services.AddScoped<ICardTypeService, CardTypeService>();
builder.Services.AddScoped<IWorldCardService, WorldCardService>();
builder.Services.AddScoped<IWorldProjectService, WorldProjectService>();
builder.Services.AddSingleton<CosService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<WatermarkService>();
builder.Services.AddScoped<ILingMaiPublishHandler, DocPublishHandler>();
builder.Services.AddScoped<ILingMaiPublishHandler, PostPublishHandler>();
builder.Services.AddScoped<ILingMaiPublishHandler, BlogPublishHandler>();
builder.Services.AddScoped<ILingMaiPublishHandler, ArtPublishHandler>();
builder.Services.AddScoped<ILingMaiPublishHandler, WikiPublishHandler>();
builder.Services.AddScoped<TradeService>();
builder.Services.AddScoped<LingMaiService>();
builder.Services.AddScoped<NoteCommentService>();
builder.Services.AddScoped<TaiChuWeb_V2.Services.Artworks.ArtworkEngagementService>();
builder.Services.AddScoped<TaiChuWeb_V2.Services.Artworks.ArtworkPublicationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<SystemConfigService>();
builder.Services.AddScoped<IWorldQuotaService, WorldQuotaService>();
builder.Services.AddScoped<WorldRestoreService>();
builder.Services.AddScoped<WorldHistoryService>(); // 世界观条目修改记录（查看 / 撤销）
builder.Services.AddHostedService<WorldRestoreCleanup>();
// 世界观协作者表（启动时自动创建）
builder.Services.AddHostedService<WorldCollaboratorSchema>();
// 我的世界服务器「世界任务」：启动时建表；结算（完成、发奖励、开启下一个）
builder.Services.AddHostedService<TaiChuWeb_V2.Services.Quest.ServerQuestSchema>();
builder.Services.AddScoped<TaiChuWeb_V2.Services.Quest.ServerQuestService>();
// 岁月史书：启动时建表
builder.Services.AddHostedService<TaiChuWeb_V2.Services.CommunityHistory.CommunityHistorySchema>();
// 举报：启动时建表
builder.Services.AddHostedService<TaiChuWeb_V2.Services.Reports.ContentReportSchema>();
// 旧博客补列表封面（正文有图但没设封面的，取第一张图）
builder.Services.AddHostedService<TaiChuWeb_V2.Services.Publish.BlogCoverBackfill>();

// 社区数据分析：在线人数、每日活跃、在线快照（启动时自动建表，见 Services/Analytics）
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddSingleton<AnalyticsStore>();
builder.Services.AddHostedService<AnalyticsWorker>();

// 社区聊天室：频道/消息/AI 人格（启动后首次使用时自动建表，见 Services/Chat）
builder.Services.AddSingleton<ChatStore>();
builder.Services.AddSingleton<ChatAiQueue>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddSingleton<BotGateway>();

// 太虚游 · Steam 游戏展示（首次使用时自动建表，见 Services/Games）
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Games.GameShowcaseService>();

// 太虚游 · 土豆（娱乐积分）、战绩记录、斗地主（数据表首次使用时自动创建）
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Games.PotatoService>();
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Games.MatchRecordService>();
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Games.Doudizhu.DdzManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TaiChuWeb_V2.Services.Games.Doudizhu.DdzManager>());
// 联机象棋
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Games.Xiangqi.XqManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TaiChuWeb_V2.Services.Games.Xiangqi.XqManager>());
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Games.Go.GoManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TaiChuWeb_V2.Services.Games.Go.GoManager>());
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Paint.PaintManager>();
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Paint.PaintBrushService>();
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Shop.ShopService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TaiChuWeb_V2.Services.Paint.PaintManager>());
// 网站背景（官方背景 / 自定义背景）与赞助特权（数据表首次使用时自动创建）
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Personalization.UserAppearanceService>();
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Personalization.SponsorPerkService>();
builder.Services.AddSingleton<TaiChuWeb_V2.Services.Personalization.ProfileDecorService>();
builder.Services.AddHostedService<ChatAiWorker>();

// 爱发电同步（订单→财政、赞助者→赞助墙）
builder.Services.AddScoped<TaiChuWeb_V2.Services.Afdian.AfdianService>();
builder.Services.AddScoped<TaiChuWeb_V2.Services.Afdian.AfdianSyncService>();
if (builder.Configuration.GetValue("Afdian:SyncEnabled", true))
    builder.Services.AddHostedService<TaiChuWeb_V2.Services.Afdian.AfdianSyncWorker>();
builder.Services.AddScoped<IProjectPermissionService, ProjectPermissionService>();



// Program.cs

var minioEndpoint = builder.Configuration["MinioSettings:Endpoint"];
var minioAccessKey = builder.Configuration["MinioSettings:AccessKey"];
var minioSecretKey = builder.Configuration["MinioSettings:SecretKey"];

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    // 强制清理 endpoint，绝对不能带协议前缀，也不能带无关端口
    var cleanEndpoint = minioEndpoint?
        .Replace("https://", "")
        .Replace("http://", "")
        .TrimEnd('/');

    // 自定义 HttpClient 处理程序，无视 Cloudflare 边缘证书或自签名证书的潜在校验报错
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
    };

    return new MinioClient()
        .WithEndpoint(cleanEndpoint)
        .WithCredentials(minioAccessKey, minioSecretKey)
        .WithSSL() // 🌟 保持开启，因为请求的是 Cloudflare 域名
        .WithHttpClient(new HttpClient(handler)) // 🌟 挂载自定义 Handler
        .Build();
});









builder.Services.AddControllers(options => {
    options.Filters.Add<GlobalExceptionFilter>();
    options.Filters.Add<TaiChuWeb_V2.Filters.AdminAccessFilter>();
})
.AddJsonOptions(options => {
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                    return Task.CompletedTask;
                }

                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    context.Token = authHeader.Substring("Bearer ".Length);
                }

                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

// 5. 配置 HTTP 管道 (注意严格的管道顺序)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// ⭐ 【关键点】：标准管道顺序，不可混淆
app.UseHttpsRedirection(); // 1. HTTPS 重定向最先处理
app.UseRouting();          // 2. 路由
app.UseCors("AllowAll");   // 3. CORS 必须放在 UseRouting 和 UseAuthentication 之间
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) }); // 聊天室机器人接入用
app.UseAuthentication();   // 4. 认证
app.UseActivityTracking(); //    记录登录用户的在线与活跃（仅内存操作）
app.UseAuthorization();    // 5. 授权

app.MapControllers();
app.MapHub<GameHub>("/signalr/game");
app.MapHub<ChatHub>("/signalr/chat");
app.MapHub<DdzHub>("/signalr/ddz");
app.MapHub<XiangqiHub>("/signalr/xiangqi");
app.MapHub<GoHub>("/signalr/go");
app.MapHub<PaintHub>("/signalr/paint");
// 外部机器人接入（太初机器人协议）：放在 /signalr 下，复用已经为 SignalR 配好的 WebSocket 反向代理
app.MapGet(BotGateway.Path, (HttpContext context, BotGateway gateway) => gateway.HandleAsync(context));

app.Run();
