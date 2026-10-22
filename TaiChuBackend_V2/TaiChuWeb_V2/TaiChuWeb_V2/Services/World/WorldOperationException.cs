namespace TaiChuWeb_V2.Services.World;

public enum WorldErrorKind { Invalid, Unauthenticated, Forbidden, NotFound, Conflict }

/// <summary>业务失败与 HTTP 映射分离；Details 保留现有配额错误的附加字段。</summary>
public sealed class WorldOperationException : Exception
{
    public WorldErrorKind Kind { get; }
    public IReadOnlyDictionary<string, object?> Details { get; }

    public WorldOperationException(WorldErrorKind kind, string message,
        IReadOnlyDictionary<string, object?>? details = null) : base(message)
    {
        Kind = kind;
        Details = details ?? new Dictionary<string, object?>();
    }
}
