namespace Nexora.SearchAPI.Pipeline;

public record ProcessedQuery(
    string OriginalQuery,
    string NormalizedQuery,
    string? CorrectedQuery,
    IReadOnlyList<string> ExpandedTerms,
    SearchIntent Intent,
    Dictionary<string, string>? IntentFilters);

public sealed class QueryPipeline(
    QuerySanitizer sanitizer,
    QueryNormalizer normalizer,
    SynonymExpander synonymExpander,
    IntentClassifier intentClassifier,
    ILogger<QueryPipeline> logger)
{
    public async Task<ProcessedQuery> ProcessAsync(string rawQuery, CancellationToken ct = default)
    {
        var sanitized = sanitizer.Sanitize(rawQuery);
        if (string.IsNullOrEmpty(sanitized))
            logger.LogWarning("Query rejected by sanitizer: {Query}", rawQuery);

        var normalized = normalizer.Normalize(sanitized);
        var expanded = await synonymExpander.ExpandAsync(normalized, ct);
        var classified = intentClassifier.Classify(normalized);

        return new ProcessedQuery(
            OriginalQuery: rawQuery,
            NormalizedQuery: normalized,
            CorrectedQuery: normalized != rawQuery.Trim().ToLowerInvariant() ? normalized : null,
            ExpandedTerms: expanded,
            Intent: classified.Intent,
            IntentFilters: classified.Filters);
    }
}
