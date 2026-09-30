using System.Text.Json.Serialization;

namespace ArmaFit_API.Models;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    [JsonPropertyName("_links")]
    public Dictionary<string, Link>? Links { get; init; }
}
