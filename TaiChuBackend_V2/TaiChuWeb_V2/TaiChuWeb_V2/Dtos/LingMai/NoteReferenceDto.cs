using System.Text.Json.Serialization;

namespace TaiChuWeb_V2.Dtos.LingMai;

public class NoteReferenceDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    [JsonPropertyName("excerpt")]
    public string? Excerpt { get; init; }
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}
