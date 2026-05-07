using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Features.Suggest;

public interface ISuggestSearchClient
{
    Task<IReadOnlyList<SuggestionItem>> SearchAsync(string prefix, int limit, string? category, CancellationToken ct);
}
