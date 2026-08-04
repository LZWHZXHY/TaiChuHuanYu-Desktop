using System.Security.Claims;

namespace TaiChuWeb_V2.Utils
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            // 尝试从常见的 Claim 类型中获取用户 ID
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? user.FindFirst("sub")?.Value
                              ?? user.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("用户未登录或 Token 中缺少用户标识");

            return Guid.Parse(userIdClaim);
        }
    }
}