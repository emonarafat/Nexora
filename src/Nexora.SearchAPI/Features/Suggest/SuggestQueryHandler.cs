using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Nexora.SearchAPI.Infrastructure;
using Nexora.SearchAPI.Pipeline;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Features.Suggest;

public sealed class SuggestQueryHandler(
    ISuggestSearchClient searchClient,
    IValkeyCache cache,
    QuerySanitizer sanitizer,
    QueryNormalizer normalizer,
    ILogger<SuggestQueryHandler> logger)
{
    public async Task<SuggestResponse> HandleAsync(SuggestRequest req, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var sanitized = sanitizer.Sanitize(req.Query);
        var normalizedPrefix = normalizer.Normalize(sanitized);
        var limit = Math.Clamp(req.Limit, 1, SearchConstants.MaxSuggestResults);
        if (string.IsNullOrWhiteSpace(normalizedPrefix) || normalizedPrefix.Length < 2)
        {
            return new SuggestResponse { Suggestions = [], LatencyMs = sw.Elapsed.TotalMilliseconds };
        }

        try
        {
            var cacheVersion = await cache.GetAsync<string>(SearchConstants.CacheKeys.SuggestVersion, ct) ?? "v1";
            var cacheKey = BuildCacheKey(normalizedPrefix, req.Category, limit, cacheVersion);
            var cached = await cache.GetAsync<List<SuggestionItem>>(cacheKey, ct);
            if (cached is not null)
            {
                return new SuggestResponse
                {
                    Suggestions = cached,
                    CacheHit = true,
                    LatencyMs = sw.Elapsed.TotalMilliseconds
                };
            }

            var suggestions = (await searchClient.SearchAsync(normalizedPrefix, limit, req.Category, ct))
                .OrderByDescending(x => x.PopularityScore)
                .ThenBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();

            await cache.SetAsync(cacheKey, suggestions, SearchConstants.CacheTtl.Suggest, ct);

            return new SuggestResponse
            {
                Suggestions = suggestions,
                CacheHit = false,
                LatencyMs = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Typesense suggest failed");
            return new SuggestResponse
            {
                Suggestions = [],
                CacheHit = false,
                LatencyMs = sw.Elapsed.TotalMilliseconds
            };
        }
    }

    public Task InvalidateCacheAsync(CancellationToken ct)
    {
        var version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        return cache.SetAsync(SearchConstants.CacheKeys.SuggestVersion, version, TimeSpan.FromDays(1), ct);
    }

    private static string BuildCacheKey(string normalizedPrefix, string? category, int limit, string version)
    {
        var raw = $"{version}|{normalizedPrefix}|{category?.Trim().ToLowerInvariant() ?? "*"}|{limit}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        return SearchConstants.CacheKeys.Suggest(hash);
    }
}
