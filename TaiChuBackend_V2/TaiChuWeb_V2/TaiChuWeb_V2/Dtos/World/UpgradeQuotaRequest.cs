using System.ComponentModel.DataAnnotations;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Dtos.World;

public class UpgradeQuotaRequest
{
    [EnumDataType(typeof(QuotaUpgradeType))]
    public QuotaUpgradeType UpgradeType { get; set; }
}
