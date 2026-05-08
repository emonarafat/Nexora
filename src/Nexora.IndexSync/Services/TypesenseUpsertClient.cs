using Microsoft.Extensions.Options;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;
using Typesense;
using Typesense.Setup;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Nexora.IndexSync.Services;

public sealed class TypesenseUpsertClient(IConfiguration config, ILogger<TypesenseUpsertClient> logger)
{
    private ITypesenseClient CreateClient()
    {
        var nodes = new List<Node>
        {
            new(
                config["Typesense:Host"] ?? "localhost",
                config["Typesense:Port"] ?? "8108",
                config["Typesense:Protocol"] ?? "http"
            )
        };
        return new TypesenseClient(OptionsFactory.Create(new Config(nodes, config["Typesense:ApiKey"] ?? "")), new HttpClient());
    }

    public async Task UpsertBatchAsync(IReadOnlyList<ProductDocument> docs, CancellationToken ct)
    {
        if (docs.Count == 0) return;
        try
        {
            var client = CreateClient();
            await client.ImportDocuments<ProductDocument>(
                SearchConstants.ProductsCollection,
                docs,
                docs.Count,
                ImportType.Upsert);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upsert batch failed ({Count} docs)", docs.Count);
            throw;
        }
    }

    public async Task DeleteBatchAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        try
        {
            var client = CreateClient();
            foreach (var id in ids)
            {
                try { await client.DeleteDocument<ProductDocument>(SearchConstants.ProductsCollection, id); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Delete failed for id {Id}", id);
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Delete batch failed ({Count} ids)", ids.Count);
            throw;
        }
    }
}
