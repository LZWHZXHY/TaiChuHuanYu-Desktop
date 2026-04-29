using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TaiChuWeb_V2.Filters
{
    public class GlobalExceptionFilter : IAsyncExceptionFilter
    {
        private readonly IWebHostEnvironment _env;

        public GlobalExceptionFilter(IWebHostEnvironment env) => _env = env;

        // 注意：方法名必须是 OnExceptionAsync
        public async Task OnExceptionAsync(ExceptionContext context)
        {
            // 构造标准错误包
            var errorResponse = new
            {
                message = "后端异常 (系统异常)",
                detail = context.Exception.Message,
                stack = _env.IsDevelopment() ? context.Exception.StackTrace : null
            };

            // 设置返回结果
            context.Result = new ObjectResult(errorResponse) { StatusCode = 500 };

            // 标记异常已处理，防止异常继续向上抛出导致程序崩溃
            context.ExceptionHandled = true;

            // 异步方法需要返回 Task
            await Task.CompletedTask;
        }
    }
}