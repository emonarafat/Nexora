using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using StackExchange.Redis;

namespace Nexora.SearchAPI.Infrastructure;

[ExcludeFromCodeCoverage]
public sealed class ValkeyCache(IConnectionMultiplexer redis, ILogger<ValkeyCache> logger) : IValkeyCache
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            return value.HasValue ? JsonSerializer.Deserialize<T>((string)value!) : null;
        }
        catch (Exception ex) { logger.LogWarning(ex, "Cache GET failed: {Key}", key); return null; }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        try { await _db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl); }
        catch (Exception ex) { logger.LogWarning(ex, "Cache SET failed: {Key}", key); }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try { await _db.KeyDeleteAsync(key); }
        catch (Exception ex) { logger.LogWarning(ex, "Cache DELETE failed: {Key}", key); }
    }
}
