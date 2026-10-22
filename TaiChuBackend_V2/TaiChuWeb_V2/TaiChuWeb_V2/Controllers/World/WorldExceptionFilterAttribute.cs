using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

/// <summary>仅处理世界观业务失败，未知异常继续交给现有全局过滤器。</summary>
public sealed class WorldExceptionFilterAttribute : ExceptionFilterAttribute
{
    // 卡片类型列表原有的 500 响应格式保留在该 action 上。
    public string? FailureMessage { get; set; }

    public override void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled) return;
        if (context.Exception is WorldValidationException validation)
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(validation.Errors) { Status = 400 });
        else if (context.Exception is WorldOperationException failure)
        {
            var status = failure.Kind switch
            {
                WorldErrorKind.Invalid => 400,
                WorldErrorKind.Unauthenticated => 401,
                WorldErrorKind.Forbidden => 403,
                WorldErrorKind.NotFound => 404,
                WorldErrorKind.Conflict => 409,
                _ => 500
            };
            var body = new Dictionary<string, object?>(failure.Details) { ["message"] = failure.Message };
            context.Result = new ObjectResult(body) { StatusCode = status };
        }
        else if (FailureMessage != null)
            context.Result = new ObjectResult(new { message = FailureMessage, error = context.Exception.Message }) { StatusCode = 500 };
        else return;

        context.ExceptionHandled = true;
    }
}
