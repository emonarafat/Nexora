using Nexora.IndexSync.Services;
using Nexora.IndexSync.Workers;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Nexora.IndexSync"))
        .AddOtlpExporter());

builder.Services.AddSingleton<TypesenseUpsertClient>();
builder.Services.AddSingleton<CdcChangeReader>();
builder.Services.AddSingleton<FieldMapper>();
builder.Services.AddSingleton<SearchApiSuggestCacheInvalidator>();
builder.Services.AddHttpClient("SearchApi", c => c.Timeout = TimeSpan.FromSeconds(2));
builder.Services.AddHostedService<CdcSyncWorker>();
builder.Services.AddHostedService<FullReindexWorker>();

var host = builder.Build();
host.Run();
