using Microsoft.Extensions.Options;
using Typesense;
using Typesense.Setup;

namespace Nexora.SearchAPI.Infrastructure;

public sealed class TypesenseClientFactory(IConfiguration config)
{
    public ITypesenseClient CreateClient()
    {
        var nodes = new List<Node>
        {
            new(
                config["Typesense:Host"] ?? "localhost",
                config["Typesense:Port"] ?? "8108",
                config["Typesense:Protocol"] ?? "http"
            )
        };
        var tsConfig = new Config(nodes, config["Typesense:ApiKey"] ?? "");
        return new TypesenseClient(Options.Create(tsConfig), new HttpClient());
    }
}
