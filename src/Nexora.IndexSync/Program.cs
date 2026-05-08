using Nexora.IndexSync.Services;
using Nexora.IndexSync.Workers;
using Nexora.IndexSync.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Nexora.IndexSync"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Nexora.IndexSync"))
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(IndexSyncMetrics.MetricsSourceName)
        .AddPrometheusExporter());

builder.Services.Configure<IndexSyncOptions>(builder.Configuration.GetSection(IndexSyncOptions.SectionName));
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IndexSyncMetrics>();
builder.Services.AddSingleton<TypesenseUpsertClient>();
builder.Services.AddSingleton<CdcChangeReader>();
builder.Services.AddSingleton<CdcQueryBuilder>();
builder.Services.AddSingleton<BatchCollector>();
builder.Services.AddSingleton<FieldMapper>();
builder.Services.AddSingleton<SyncRetryPolicy>();
builder.Services.AddSingleton<SyncDeadLetterWriter>();
builder.Services.AddSingleton<SyncBatchProcessor>();
builder.Services.AddSingleton<FullReindexSignal>();
builder.Services.AddSingleton<SearchApiSuggestCacheInvalidator>();
builder.Services.AddHttpClient("SearchApi", c => c.Timeout = TimeSpan.FromSeconds(2));
builder.Services.AddHostedService<CdcSyncWorker>();
builder.Services.AddHostedService<FullReindexWorker>();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint("/metrics");
app.MapPost("/internal/reindex", (FullReindexSignal signal, ILogger<Program> logger) =>
{
    signal.Trigger();
    logger.LogInformation("Full re-index requested via internal endpoint");
    return Results.Accepted("/internal/reindex", new { Status = "Accepted" });
});

app.Run();
