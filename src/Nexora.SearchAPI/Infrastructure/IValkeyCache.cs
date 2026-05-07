namespace Nexora.SearchAPI.Infrastructure;

public interface IValkeyCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class;
    Task DeleteAsync(string key, CancellationToken ct = default);
}
