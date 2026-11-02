using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Resources;

/// <summary>
/// 经验表（UserStats）和经验流水表（UserExpLog）在数据库里的真实表名、列名。
/// 资源购买要在一个原生 SQL 事务里「扣下载者经验、加上传者经验、记购买、记流水」，
/// 所以和 PotatoService 的经验兑换一样，从 EF 模型里取名字，避免和数据库命名不一致。
/// </summary>
public sealed record ExpTableNames(string Stats, string StatsUser, string StatsExp, string Log, string LogUser, string LogChange, string LogReason, string LogCreated)
{
    private static ExpTableNames? _cached;

    public static ExpTableNames From(AppDbContext db)
    {
        if (_cached != null) return _cached;
        var stats = db.Model.FindEntityType(typeof(TaiChuWeb_V2.Models.User.UserStats))!;
        var log = db.Model.FindEntityType(typeof(TaiChuWeb_V2.Models.User.UserExpLog))!;
        static string Col(Microsoft.EntityFrameworkCore.Metadata.IEntityType t, string prop) => t.FindProperty(prop)!.GetColumnName();
        _cached = new ExpTableNames(stats.GetTableName()!, Col(stats, "UserId"), Col(stats, "Experience"),
            log.GetTableName()!, Col(log, "UserId"), Col(log, "Change"), Col(log, "Reason"), Col(log, "CreatedAt"));
        return _cached;
    }
}
