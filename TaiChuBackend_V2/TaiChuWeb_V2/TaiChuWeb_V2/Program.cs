using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore; // 如果这行报错，说明还没执行 dotnet add package Scalar.AspNetCore
using System.Text;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Filters;
using TaiChuWeb_V2.Services.Email;
using TaiChuWeb_V2.Services.LingMai;
using TaiChuWeb_V2.Services.Publish;
using TaiChuWeb_V2.Services.Trade;
var builder = WebApplication.CreateBuilder(args);

// 1. 获取连接字符串
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. 数据库配置（手动指定版本以避开反射异常）
var serverVersion = new MySqlServerVersion(new Version(8, 0, 31));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
        mySqlOptions.EnableRetryOnFailure(3)
    ));

// 3. 核心服务
builder.Services.AddControllers();
builder.Services.AddOpenApi(); // .NET 10 原生 API 文档支持

// 4. 配置跨域（供 Vue3 访问）
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => 
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});



builder.Services.AddScoped<ILingMaiPublishHandler, ArtPublishHandler>();
builder.Services.AddScoped<ILingMaiPublishHandler, WikiPublishHandler>();
builder.Services.AddScoped<TradeService>();
builder.Services.AddScoped<LingMaiService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<JwtService>();
// Program.cs
builder.Services.AddControllers(options => {
    // 保持你原有的全局异常过滤逻辑
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
    });

var app = builder.Build();

// 5. 配置 HTTP 管道 (这里已经删除了所有 UseSwagger 代码)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // 生成文档节点
    app.MapScalarApiReference(); // 生成极其漂亮的 Scalar 调试界面
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthentication(); // 👈 必须加上这行！不然你的 CurrentUserId 永远是 null 导致接口报 401 错误
app.UseAuthorization();
app.MapControllers();

app.Run();