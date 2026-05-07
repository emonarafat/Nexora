namespace Nexora.Shared.DTOs;

public record SuggestRequest
{
    public string Query { get; init; } = string.Empty;
    public int Limit { get; init; } = 8;
    public string? Category { get; init; }
}
