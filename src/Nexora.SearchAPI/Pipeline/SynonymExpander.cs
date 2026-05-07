using Microsoft.Extensions.Caching.Memory;
using Nexora.Shared.Constants;
using Npgsql;

namespace Nexora.SearchAPI.Pipeline;

public sealed class SynonymExpander(IConfiguration config, ILogger<SynonymExpander> logger)
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public async Task<IReadOnlyList<string>> ExpandAsync(string query, CancellationToken ct = default)
    {
        var map = await GetMapAsync(ct);
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { query };

        foreach (var word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (map.TryGetValue(word, out var syns))
                foreach (var s in syns)
                    expanded.Add(query.Replace(word, s, StringComparison.OrdinalIgnoreCase));
        }

        if (map.TryGetValue(query, out var fullSyns))
            foreach (var s in fullSyns) expanded.Add(s);

        return [.. expanded];
    }

    private async Task<Dictionary<string, List<string>>> GetMapAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue("synonyms", out Dictionary<string, List<string>>? cached) && cached != null)
            return cached;

        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var connStr = config.GetConnectionString("Postgres");
        if (!string.IsNullOrEmpty(connStr))
        {
            try
            {
                await using var conn = new NpgsqlConnection(connStr);
                await conn.OpenAsync(ct);
                await using var cmd = new NpgsqlCommand(
                    "SELECT term, synonyms FROM search_synonyms WHERE is_active = true", conn);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    map[reader.GetString(0)] = [.. reader.GetFieldValue<string[]>(1)];
            }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to load synonyms"); }
        }

        _cache.Set("synonyms", map, SearchConstants.CacheTtl.Synonyms);
        return map;
    }
}
