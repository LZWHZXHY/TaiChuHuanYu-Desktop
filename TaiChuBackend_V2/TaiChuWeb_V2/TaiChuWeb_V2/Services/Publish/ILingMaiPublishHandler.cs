// TaiChuWeb_V2/Services/Publish/ILingMaiPublishHandler.cs
using Microsoft.AspNetCore.Mvc;

namespace TaiChuWeb_V2.Services.Publish
{
    public interface ILingMaiPublishHandler
    {

        string SupportType { get; }


        Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId, string? projectId = null);
    }
}