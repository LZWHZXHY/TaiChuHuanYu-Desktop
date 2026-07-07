using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaiChuWeb_V2.Dtos.World;


namespace TaiChuWeb_V2.Services.World
{
    public interface IWorldCardService
    {
        Task<IEnumerable<CardResponseDto>> GetCardsByProjectAsync(Guid projectId, Guid userId, string? typeFilter = null);
        Task<CardResponseDto> GetCardByIdAsync(Guid cardId, Guid userId);
        Task<CardResponseDto> CreateCardAsync(Guid projectId, Guid userId, CreateCardDto dto);
        Task<CardResponseDto> UpdateCardAsync(Guid cardId, Guid userId, UpdateCardDto dto);
        Task<bool> DeleteCardAsync(Guid cardId, Guid userId);
        Task<bool> IsCardInProjectAsync(Guid cardId, Guid projectId);
    }
}