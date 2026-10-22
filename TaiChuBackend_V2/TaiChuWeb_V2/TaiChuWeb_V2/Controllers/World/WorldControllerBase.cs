using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

[ApiController]
[WorldExceptionFilter]
public abstract class WorldControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty
            ? id
            : throw new WorldOperationException(WorldErrorKind.Unauthenticated, "用户未认证");
}
