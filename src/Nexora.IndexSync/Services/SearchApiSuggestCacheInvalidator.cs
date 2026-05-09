using System.Diagnostics.CodeAnalysis;

namespace Nexora.IndexSync.Services;

[ExcludeFromCodeCoverage]
public sealed class SearchApiSuggestCacheInvalidator(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<SearchApiSuggestCacheInvalidator> logger)
{
    private const string InvalidationPath = "/api/v1/suggest/cache/invalidate";

    public async Task InvalidateAsync(CancellationToken ct)
    {
        var baseUrl = config["SearchApi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}{InvalidationPath}");
            var response = await httpClientFactory.CreateClient("SearchApi").SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Suggest cache invalidation failed with status code {StatusCode}", response.StatusCode);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Suggest cache invalidation request timed out");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Suggest cache invalidation request failed");
        }
    }
}
