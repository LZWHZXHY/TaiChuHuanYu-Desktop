using Microsoft.AspNetCore.Mvc;

namespace TaiChuWeb_V2.Utils
{
    /// <summary>
    /// 接口参数校验没通过时（必填项为空、数字填成文字、请求体是空的……），ASP.NET 默认返回英文：
    /// 「The Password field is required.」「One or more validation errors occurred.」，用户看不懂。
    /// 这里统一换成中文的 { message }，前端直接显示。
    /// </summary>
    public static class ValidationMessages
    {
        private static readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase)
        {
            ["username"] = "名号", ["password"] = "密码", ["newpassword"] = "新密码", ["email"] = "邮箱",
            ["verificationcode"] = "验证码", ["identifier"] = "名号或邮箱", ["title"] = "标题", ["content"] = "内容",
            ["name"] = "名称", ["description"] = "简介", ["amount"] = "数量", ["price"] = "价格", ["code"] = "代码",
        };

        /// <summary>字段名转中文（"$.password"、"dto.Password"、"Password" 都认）</summary>
        public static string FieldName(string key)
        {
            var k = key.Split('.', '[').Where(s => s.Length > 0 && s != "$").LastOrDefault() ?? "";
            return Fields.TryGetValue(k.TrimEnd(']'), out var zh) ? zh : "填写的内容";
        }

        public static IActionResult Response(ActionContext context)
        {
            var bad = context.ModelState.Where(kv => kv.Value != null && kv.Value.Errors.Count > 0).ToList();
            var messages = new List<string>();
            foreach (var (key, entry) in bad)
            {
                var field = FieldName(key);
                foreach (var e in entry!.Errors)
                {
                    var raw = e.ErrorMessage ?? e.Exception?.Message ?? "";
                    var text =
                        raw.Contains("non-empty request body", StringComparison.OrdinalIgnoreCase) ? "提交的内容是空的，请刷新页面再试"
                        : raw.Contains("required", StringComparison.OrdinalIgnoreCase) && field == "填写的内容" ? "还有必填的内容没填"
                        : raw.Contains("required", StringComparison.OrdinalIgnoreCase) ? $"请填写{field}"
                        : raw.Contains("JSON", StringComparison.OrdinalIgnoreCase) || raw.Contains("converted", StringComparison.OrdinalIgnoreCase) || e.Exception != null ? $"{field}的格式不对"
                        : raw.Contains("length", StringComparison.OrdinalIgnoreCase) ? $"{field}的长度不对"
                        : $"{field}不对，请检查一下";
                    if (!messages.Contains(text)) messages.Add(text);
                }
            }
            if (messages.Count == 0) messages.Add("提交的内容有问题，请检查后再试");
            return new BadRequestObjectResult(new { message = string.Join("；", messages.Take(3)) });
        }
    }
}
