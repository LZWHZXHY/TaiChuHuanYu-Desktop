using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Services.World;

public static class WorldValidation
{
    // HTTP 使用 ApiController 的自动校验；服务直接调用复用同一组 DTO 约束。
    public static void Validate(object value)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(value, new ValidationContext(value), results, true)) return;
        var errors = results.SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => (member, message: result.ErrorMessage ?? "参数无效")))
            .GroupBy(item => item.member)
            .ToDictionary(group => group.Key, group => group.Select(item => item.message).ToArray());
        throw new WorldValidationException(errors);
    }
}

public sealed class WorldValidationException(Dictionary<string, string[]> errors) : Exception("参数校验失败")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
