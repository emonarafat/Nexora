namespace Nexora.Shared.DTOs;

public record SearchRequest
{
    public string Query { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 20;
    public string Sort { get; init; } = "relevance";
    public string? FilterBy { get; init; }
    public string? FacetBy { get; init; }
}
