using System.Collections.Generic;
using System.Threading.Tasks;
using TaiChuWeb_V2.Dtos.World;


namespace TaiChuWeb_V2.Services.World
{
    public interface ICardTypeService
    {
        /// <summary>
        /// 获取所有激活的卡片类型（按排序顺序）
        /// </summary>
        Task<IEnumerable<CardTypeDto>> GetAllActiveTypesAsync();

        /// <summary>
        /// 获取所有卡片类型（包括未激活的）
        /// </summary>
        Task<IEnumerable<CardTypeDto>> GetAllTypesAsync();

        /// <summary>
        /// 根据ID获取卡片类型
        /// </summary>
        Task<CardTypeDto> GetTypeByIdAsync(string id);

        /// <summary>
        /// 创建新卡片类型
        /// </summary>
        Task<CardTypeDto> CreateTypeAsync(CreateCardTypeDto dto);

        /// <summary>
        /// 更新卡片类型
        /// </summary>
        Task<CardTypeDto> UpdateTypeAsync(string id, UpdateCardTypeDto dto);

        /// <summary>
        /// 删除卡片类型（仅非系统类型可删除）
        /// </summary>
        Task<bool> DeleteTypeAsync(string id);
    }
}