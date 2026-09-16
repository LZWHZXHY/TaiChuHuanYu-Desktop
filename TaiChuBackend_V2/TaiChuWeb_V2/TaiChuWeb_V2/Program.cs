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
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<SystemConfigService>();
builder.Services.AddScoped<IWorldQuotaService, WorldQuotaService>();
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
app.UseAuthentication();   // 4. 认证
app.UseAuthorization();    // 5. 授权

app.MapControllers();
app.MapHub<GameHub>("/signalr/game");

app.Run();