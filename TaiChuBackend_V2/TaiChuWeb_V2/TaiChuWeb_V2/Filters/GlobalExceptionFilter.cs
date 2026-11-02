using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TaiChuWeb_V2.Filters
{
    /// <summary>
    /// 接口里没被处理的异常：给用户一句看得懂的话 + 一个错误编号，详细错误写进服务器日志（按编号能查到）。
    /// 以前统一返回「后端异常 (系统异常)」，前端就只显示这一句，用户和管理员都不知道是什么问题。
    /// 开发环境（本机调试）仍然把异常信息和堆栈带回去，方便排查。
    /// </summary>
    public class GlobalExceptionFilter : IAsyncExceptionFilter
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<GlobalExceptionFilter> _logger;

        public GlobalExceptionFilter(IWebHostEnvironment env, ILogger<GlobalExceptionFilter> logger)
        {
            _env = env;
            _logger = logger;
        }

        // 注意：方法名必须是 OnExceptionAsync
        public Task OnExceptionAsync(ExceptionContext context)
        {
            var ex = context.Exception;
            // 用户自己取消（关页面、切走）：不算错误
            if (ex is OperationCanceledException && context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                context.Result = new StatusCodeResult(499);
                context.ExceptionHandled = true;
                return Task.CompletedTask;
            }

            // 错误编号：8 位，用户截图或报给管理员时带上，管理员在日志里搜这个编号
            var errorId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            _logger.LogError(ex, "接口出错 [{ErrorId}] {Method} {Path}", errorId,
                context.HttpContext.Request.Method, context.HttpContext.Request.Path);

            var (status, message) = ex switch
            {
                TimeoutException => (504, "服务器处理超时了，请稍后再试"),
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (409, "数据刚被别人改过了，请刷新后再试"),
                Microsoft.EntityFrameworkCore.DbUpdateException => (500, "保存数据时出错了，请稍后再试"),
                _ => (500, "服务器出错了，请稍后再试"),
            };

            context.Result = new ObjectResult(new
            {
                message = $"{message}。一直这样的话，请把错误编号 {errorId} 告诉管理员",
                errorId,
                detail = _env.IsDevelopment() ? ex.Message : null,
                stack = _env.IsDevelopment() ? ex.StackTrace : null,
            })
            { StatusCode = status };

            // 标记异常已处理，防止异常继续向上抛出导致程序崩溃
            context.ExceptionHandled = true;
            return Task.CompletedTask;
        }
    }
}
