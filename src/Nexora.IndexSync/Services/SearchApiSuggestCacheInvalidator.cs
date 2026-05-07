namespace Nexora.IndexSync.Services;

public sealed class SearchApiSuggestCacheInvalidator(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<SearchApiSuggestCacheInvalidator> logger)
{
    public async Task InvalidateAsync(CancellationToken ct)
    {
        var baseUrl = config["SearchApi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/v1/suggest/cache/invalidate");
            var response = await httpClientFactory.CreateClient("SearchApi").SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Suggest cache invalidation failed with status code {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Suggest cache invalidation request failed");
        }
    }
}
