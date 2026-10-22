using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaiChuWeb_V2.Controllers.World;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Filters;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.World;

internal static class ContractChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var relationId = Guid.NewGuid();
        var project = new ProjectResponseDto { Id = projectId, Name = "世界", OwnerId = userId };
        var card = new CardResponseDto { Id = cardId, ProjectId = projectId, Title = "角色" };
        var type = new CardTypeDto { Id = "character", Label = "角色", IsActive = true };
        var relation = new RelationDto { Id = relationId, SourceCardId = cardId };
        var mode = "normal";
        object?[]? lastArguments = null;
        var calls = 0;
        object? Handle(string method, object?[] args)
        {
            calls++;
            lastArguments = args;
            if (mode == "conflict") throw new WorldOperationException(WorldErrorKind.Conflict, "重复类型");
            if (mode == "forbidden") throw new WorldOperationException(WorldErrorKind.Forbidden, "源卡片不存在或无权限");
            if (mode == "invalid") throw new WorldOperationException(WorldErrorKind.Invalid, "不能关联自己");
            if (mode == "private") throw new WorldOperationException(WorldErrorKind.NotFound, "项目不存在或无权访问");
            if (mode == "quota") throw new WorldOperationException(WorldErrorKind.Invalid, "配额不足", new Dictionary<string, object?>
            {
                ["success"] = false, ["code"] = method == "CreateCardAsync" ? "CARD_LIMIT_EXCEEDED" : "QUOTA_EXCEEDED",
                ["currentCount"] = 10, ["maxCount"] = 10, ["used"] = 3, ["max"] = 3
            });
            if (mode == "validation") throw new WorldValidationException(new() { ["Name"] = ["参数无效"] });
            if (mode == "unexpected") throw new InvalidOperationException("test unexpected failure");
            return method switch
            {
                "GetUserProjectsAsync" or "GetPublicProjectsAsync" => Task.FromResult<IEnumerable<ProjectResponseDto>>([project]),
                "GetProjectByIdAsync" or "CreateProjectAsync" or "UpdateProjectAsync" =>
                    Task.FromResult(mode == "missing" ? null : project),
                "GetCardSummariesByProjectAsync" => Task.FromResult<IEnumerable<CardSummaryDto>>([new() { Id = cardId, ProjectId = projectId }]),
                "GetCardByIdAsync" or "CreateCardAsync" or "UpdateCardAsync" => Task.FromResult(mode == "missing" ? null : card),
                "DeleteProjectAsync" or "DeleteCardAsync" or "DeleteRelationAsync" or "DeleteTypeAsync" => Task.FromResult(mode != "missing"),
                "GetRelationsForCardAsync" or "GetRelationsForProjectAsync" => Task.FromResult<IEnumerable<RelationDto>>([relation]),
                "CreateRelationAsync" => Task.FromResult(relation),
                "GetAllActiveTypesAsync" => Task.FromResult<IEnumerable<CardTypeDto>>([type]),
                "GetTypeByIdAsync" or "CreateTypeAsync" or "UpdateTypeAsync" => Task.FromResult(mode == "missing" ? null : type),
                "GetUserStatsAsync" => Task.FromResult(new UserStats { UsedWorldCount = 1, MaxWorldCount = 3, MaxCardsPerWorld = 10, Experience = 100 }),
                "CanCreateProjectAsync" => Task.FromResult((true, "可创建", 1, 3)),
                "CanAddCardAsync" => Task.FromResult((true, "可添加", 1, 10)),
                "UpgradeQuotaAsync" => Task.FromResult(new QuotaUpgradeResult { Success = mode != "insufficient", Message = "扩容结果", RemainingExp = 10, UpgradeTypeName = "世界观数量" }),
                "GetUpgradeHistoryAsync" => Task.FromResult(new List<QuotaUpgradeRecord>()),
                _ => throw new InvalidOperationException("Unexpected service call: " + method)
            };
        }

        // 独立最小宿主：不执行业务 Program、不读取 appsettings、不连接邮件/对象存储/数据库。
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            ContentRootPath = Directory.GetCurrentDirectory(), EnvironmentName = "Testing"
        });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(ServiceStub<IWorldProjectService>.Create(Handle));
        builder.Services.AddSingleton(ServiceStub<IWorldCardService>.Create(Handle));
        builder.Services.AddSingleton(ServiceStub<IWorldRelationService>.Create(Handle));
        builder.Services.AddSingleton(ServiceStub<IWorldQuotaService>.Create(Handle));
        builder.Services.AddSingleton(ServiceStub<ICardTypeService>.Create(Handle));
        builder.Services.AddControllers(options => options.Filters.Add<GlobalExceptionFilter>())
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .ConfigureApplicationPartManager(manager =>
            {
                manager.ApplicationParts.Clear();
                manager.FeatureProviders.Clear();
                manager.FeatureProviders.Add(new WorldControllersOnly());
            });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            async Task<(HttpStatusCode Status, JsonElement Body, string? Location)> Send(
                HttpMethod method, string url, object? body = null, string? identity = null, bool admin = false)
            {
                using var request = new HttpRequestMessage(method, url);
                if (body != null) request.Content = JsonContent.Create(body);
                if (identity != null) request.Headers.Add("X-Test-User", identity);
                if (admin) request.Headers.Add("X-Test-Role", "Admin");
                using var response = await client.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                return (response.StatusCode, string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<JsonElement>(json),
                    response.Headers.Location?.ToString());
            }
            var uid = userId.ToString();
            var projectPath = $"/api/world/projects/{projectId}";
            var cardsPath = projectPath + "/cards";
            var relationsPath = $"/api/world/cards/{cardId}/relations";
            var response = await Send(HttpMethod.Get, "/api/world/projects/public");
            check(response.Status == HttpStatusCode.OK && response.Body[0].GetProperty("ownerId").GetGuid() == userId,
                "公开项目允许匿名访问且直接返回 DTO 数组");
            response = await Send(HttpMethod.Get, "/api/world/projects");
            check(response.Status == HttpStatusCode.Unauthorized, "我的世界观仍要求登录");
            response = await Send(HttpMethod.Get, "/api/world/projects", identity: "not-a-guid");
            check(response.Status == HttpStatusCode.Unauthorized && response.Body.GetProperty("message").GetString() == "用户未认证",
                "无效身份返回 401，不再产生 Guid.Parse 的 500");
            response = await Send(HttpMethod.Get, "/api/world/projects", identity: uid);
            check(response.Status == HttpStatusCode.OK && (Guid)lastArguments![0]! == userId, "统一解析用户身份并传入服务");

            response = await Send(HttpMethod.Post, "/api/world/projects", new { name = "世界" }, uid);
            check(response.Status == HttpStatusCode.Created && response.Location!.EndsWith(projectPath) &&
                  response.Body.GetProperty("id").GetGuid() == projectId, "项目创建保留 201、Location 和 DTO 字段");
            response = await Send(HttpMethod.Post, cardsPath, new { title = "角色", type = "character" }, uid);
            check(response.Status == HttpStatusCode.Created && response.Location!.EndsWith(cardsPath + "/" + cardId),
                "卡片创建保留 201 和详情 Location");
            response = await Send(HttpMethod.Post, relationsPath, new { targetCardId = Guid.NewGuid(), relationType = "朋友" }, uid);
            check(response.Status == HttpStatusCode.Created && response.Location!.EndsWith(relationsPath), "关系创建保留 201 和 Location");
            response = await Send(HttpMethod.Put, cardsPath + "/" + cardId, new { title = "新标题" }, uid);
            check(response.Status == HttpStatusCode.OK && (Guid?)lastArguments![3] == projectId, "卡片更新将 URL 项目归属传给服务");
            response = await Send(HttpMethod.Delete, cardsPath + "/" + cardId, identity: uid);
            check(response.Status == HttpStatusCode.NoContent && response.Body.ValueKind == JsonValueKind.Undefined &&
                (Guid?)lastArguments![2] == projectId, "卡片删除保留空 204 并传递项目归属");
            response = await Send(HttpMethod.Delete, relationsPath + "/" + relationId, identity: uid);
            check(response.Status == HttpStatusCode.NoContent && (Guid?)lastArguments![2] == cardId, "关系删除将 URL 卡片归属传给服务");
            response = await Send(HttpMethod.Get, projectPath + "/relations", identity: uid);
            check(response.Status == HttpStatusCode.OK && (Guid)lastArguments![1]! == userId, "项目关系读取必须将访问身份传给服务");

            mode = "missing";
            response = await Send(HttpMethod.Get, projectPath, identity: uid);
            check(response.Status == HttpStatusCode.NotFound && response.Body.GetProperty("message").GetString() == "项目不存在或无权访问", "项目不可访问保留原有 404 消息");
            response = await Send(HttpMethod.Delete, relationsPath + "/" + relationId, identity: uid);
            check(response.Status == HttpStatusCode.NotFound && response.Body.GetProperty("message").GetString() == "关联不存在或无权删除", "关系删除失败保留原有 404 消息");
            mode = "private";
            response = await Send(HttpMethod.Get, cardsPath, identity: uid);
            check(response.Status == HttpStatusCode.NotFound, "卡片列表的业务拒绝由世界观过滤器处理，不落入全局 500");
            mode = "forbidden";
            response = await Send(HttpMethod.Post, relationsPath, new { targetCardId = Guid.NewGuid() }, uid);
            check(response.Status == HttpStatusCode.Forbidden && response.Body.GetProperty("message").GetString() == "源卡片不存在或无权限", "权限拒绝返回 JSON 403，不将错误消息当成认证方案");
            mode = "invalid";
            response = await Send(HttpMethod.Post, relationsPath, new { targetCardId = Guid.NewGuid() }, uid);
            check(response.Status == HttpStatusCode.BadRequest && response.Body.GetProperty("message").GetString() == "不能关联自己", "关系业务校验保留 message 和 400");
            mode = "quota";
            response = await Send(HttpMethod.Post, cardsPath, new { title = "角色", type = "character" }, uid);
            check(response.Status == HttpStatusCode.BadRequest && response.Body.GetProperty("code").GetString() == "CARD_LIMIT_EXCEEDED" &&
                !response.Body.GetProperty("success").GetBoolean() && response.Body.GetProperty("currentCount").GetInt32() == 10,
                "卡片配额错误保留 code/success/currentCount/maxCount");
            response = await Send(HttpMethod.Post, "/api/world/projects", new { name = "世界" }, uid);
            check(response.Status == HttpStatusCode.BadRequest && response.Body.GetProperty("code").GetString() == "QUOTA_EXCEEDED" &&
                response.Body.GetProperty("used").GetInt32() == 3, "项目配额错误保留 code/used/max");

            mode = "normal";
            var beforeCalls = calls;
            response = await Send(HttpMethod.Post, "/api/world/projects", new { name = " " }, uid);
            check(response.Status == HttpStatusCode.BadRequest && response.Body.TryGetProperty("errors", out _) && calls == beforeCalls,
                "ApiController 在进入服务前拒绝空名称，保留 ValidationProblemDetails");
            response = await Send(HttpMethod.Put, cardsPath + "/" + cardId, new { title = new string('x', 201) }, uid);
            check(response.Status == HttpStatusCode.BadRequest && response.Body.GetProperty("errors").TryGetProperty("Title", out _), "更新卡片过长标题返回字段校验错误");
            response = await Send(HttpMethod.Post, relationsPath, new { targetCardId = Guid.NewGuid(), relationType = new string('x', 101) }, uid);
            check(response.Status == HttpStatusCode.BadRequest, "关系名称超过数据库长度时提前返回 400");
            response = await Send(HttpMethod.Post, "/api/world/quota/upgrade", new { upgradeType = 99 }, uid);
            check(response.Status == HttpStatusCode.BadRequest, "未知扩容枚举拒绝处理");
            response = await Send(HttpMethod.Get, "/api/world/quota/history?limit=-1", identity: uid);
            check(response.Status == HttpStatusCode.BadRequest, "历史查询拒绝负数 limit");
            response = await Send(HttpMethod.Get, "/api/world/quota/history?limit=0", identity: uid);
            check(response.Status == HttpStatusCode.OK && (int)lastArguments![1]! == 0, "保留 limit=0 返回空列表的兼容行为");
            response = await Send(HttpMethod.Post, "/api/world/quota/upgrade", new { upgradeType = "WorldCount" }, uid);
            check(response.Status == HttpStatusCode.OK && response.Body.GetProperty("success").GetBoolean(), "扩容保留字符串枚举和成功结果字段");
            mode = "insufficient";
            response = await Send(HttpMethod.Post, "/api/world/quota/upgrade", new { upgradeType = "WorldCardCapacity" }, uid);
            check(response.Status == HttpStatusCode.BadRequest && !response.Body.GetProperty("success").GetBoolean() &&
                response.Body.GetProperty("remainingExp").GetInt32() == 10, "经验不足保留 remainingExp 和 400");
            mode = "validation";
            response = await Send(HttpMethod.Get, projectPath, identity: uid);
            check(response.Status == HttpStatusCode.BadRequest && response.Body.GetProperty("errors").TryGetProperty("Name", out _), "服务校验与 HTTP 校验使用一致的 errors 格式");

            mode = "normal";
            response = await Send(HttpMethod.Get, "/api/world/card-types");
            check(response.Status == HttpStatusCode.OK, "卡片类型列表仍允许匿名访问");
            response = await Send(HttpMethod.Get, "/api/world/card-types/character");
            check(response.Status == HttpStatusCode.OK && response.Body.GetProperty("id").GetString() == "character", "卡片类型详情仍允许匿名访问");
            response = await Send(HttpMethod.Post, "/api/world/card-types", new { id = "custom", label = "自定义" });
            check(response.Status == HttpStatusCode.Unauthorized, "匿名用户不能创建类型");
            response = await Send(HttpMethod.Post, "/api/world/card-types", new { id = "custom", label = "自定义" }, uid);
            check(response.Status == HttpStatusCode.Forbidden, "普通用户不能创建类型");
            response = await Send(HttpMethod.Post, "/api/world/card-types", new { id = "custom", label = "自定义" }, uid, true);
            check(response.Status == HttpStatusCode.Created && response.Body.TryGetProperty("isSystem", out _) &&
                response.Body.TryGetProperty("createdAt", out _), "管理员类型创建保留 201 和实体同名字段");
            response = await Send(HttpMethod.Put, "/api/world/card-types/character", new { icon = "图" }, uid);
            check(response.Status == HttpStatusCode.Forbidden, "普通用户不能修改类型");
            response = await Send(HttpMethod.Delete, "/api/world/card-types/character", identity: uid);
            check(response.Status == HttpStatusCode.Forbidden, "普通用户不能删除类型");
            mode = "conflict";
            response = await Send(HttpMethod.Post, "/api/world/card-types", new { id = "custom", label = "自定义" }, uid, true);
            check(response.Status == HttpStatusCode.Conflict && response.Body.GetProperty("message").GetString() == "重复类型", "类型冲突保留 409 和 message");
            mode = "unexpected";
            response = await Send(HttpMethod.Get, "/api/world/card-types");
            check(response.Status == HttpStatusCode.InternalServerError && response.Body.GetProperty("message").GetString() == "获取卡片类型失败" &&
                response.Body.TryGetProperty("error", out _), "类型列表的既有 500 格式保持兼容");
            response = await Send(HttpMethod.Get, "/api/world/projects", identity: uid);
            check(response.Status == HttpStatusCode.InternalServerError && response.Body.TryGetProperty("detail", out _), "未知异常继续由现有全局过滤器处理");
        }
        finally { await app.StopAsync(); }
    }

    private sealed class WorldControllersOnly : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var type in new[] { typeof(WorldProjectsController), typeof(WorldCardsController),
                typeof(WorldRelationsController), typeof(WorldQuotaController), typeof(WorldCardTypesController) })
                feature.Controllers.Add(type.GetTypeInfo());
        }
    }
}

public class ServiceStub<T> : DispatchProxy where T : class
{
    public Func<string, object?[], object?> Handler { get; set; } = null!;
    public static T Create(Func<string, object?[], object?> handler)
    {
        var proxy = Create<T, ServiceStub<T>>();
        ((ServiceStub<T>)(object)proxy).Handler = handler;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!.Name, args ?? []);
}

public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var user)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.ToString()) };
        if (Request.Headers.TryGetValue("X-Test-Role", out var role)) claims.Add(new(ClaimTypes.Role, role.ToString()));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}
