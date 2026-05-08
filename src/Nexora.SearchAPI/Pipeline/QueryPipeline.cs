using System.Diagnostics;

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
    private static readonly ActivitySource ActivitySource = new("Nexora.SearchAPI.Pipeline");

    public async Task<ProcessedQuery> ProcessAsync(string rawQuery, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("QueryPipeline.Process");
        activity?.SetTag("query.original", rawQuery);

        // Stage 1: Sanitization
        using var sanitizeActivity = ActivitySource.StartActivity("QueryPipeline.Sanitize");
        var sanitized = sanitizer.Sanitize(rawQuery);
        sanitizeActivity?.SetTag("query.sanitized", sanitized);

        if (string.IsNullOrEmpty(sanitized))
        {
            logger.LogWarning("Query rejected by sanitizer: {Query}", rawQuery);
            sanitizeActivity?.SetTag("query.rejected", true);
        }

        // Stage 2: Normalization
        using var normalizeActivity = ActivitySource.StartActivity("QueryPipeline.Normalize");
        var normalized = normalizer.Normalize(sanitized);
        normalizeActivity?.SetTag("query.normalized", normalized);

        // Stage 3: Synonym Expansion
        IReadOnlyList<string> expanded;
        using (var expandActivity = ActivitySource.StartActivity("QueryPipeline.ExpandSynonyms"))
        {
            expanded = await synonymExpander.ExpandAsync(normalized, ct);
            expandActivity?.SetTag("query.expanded_count", expanded.Count);
        }

        // Stage 4: Intent Classification
        ClassifiedIntent classified;
        using (var classifyActivity = ActivitySource.StartActivity("QueryPipeline.ClassifyIntent"))
        {
            classified = intentClassifier.Classify(normalized);
            classifyActivity?.SetTag("query.intent", classified.Intent.ToString());
        }

        var result = new ProcessedQuery(
            OriginalQuery: rawQuery,
            NormalizedQuery: normalized,
            CorrectedQuery: !normalized.Equals(rawQuery.Trim(), StringComparison.InvariantCultureIgnoreCase) ? normalized : null,
            ExpandedTerms: expanded,
            Intent: classified.Intent,
            IntentFilters: classified.Filters);

        activity?.SetTag("query.final_intent", result.Intent.ToString());
        return result;
    }
}
