using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Nexora.SearchAPI.Features.Search;
using Nexora.SearchAPI.Features.Suggest;
using Nexora.SearchAPI.Infrastructure;
using Nexora.SearchAPI.Pipeline;
using Nexora.SearchAPI.Ranking;
using Nexora.SearchAPI.Security;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Nexora.SearchAPI"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("Nexora.SearchAPI.Pipeline")
        .AddOtlpExporter())
    .WithMetrics(m => m
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Nexora.SearchAPI"))
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        var jwtKey = jwt["Key"];
        var allowRelaxedJwtValidation = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test");

        if (string.IsNullOrWhiteSpace(jwtKey) && !allowRelaxedJwtValidation)
            throw new InvalidOperationException("JWT Key not configured.");

        o.TokenValidationParameters = string.IsNullOrWhiteSpace(jwtKey)
            ? new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = false
            }
            : new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    // Search endpoint: 60 req/min per API key (Phase 1.10 requirement)
    o.AddSlidingWindowLimiter("SearchLimit", l =>
    {
        l.Window = TimeSpan.FromMinutes(1);
        l.SegmentsPerWindow = 6;
        l.PermitLimit = 60;
        l.QueueLimit = 0;
    });

    // Suggest endpoint: 120 req/min per API key (Phase 1.10 requirement)
    o.AddSlidingWindowLimiter("SuggestLimit", l =>
    {
        l.Window = TimeSpan.FromMinutes(1);
        l.SegmentsPerWindow = 6;
        l.PermitLimit = 120;
        l.QueueLimit = 0;
    });

    // Admin operations: 30 req/min per user (Phase 1.10 requirement)
    o.AddSlidingWindowLimiter("AdminLimit", l =>
    {
        l.Window = TimeSpan.FromMinutes(1);
        l.SegmentsPerWindow = 6;
        l.PermitLimit = 30;
        l.QueueLimit = 0;
    });
});

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Valkey") ?? "localhost:6379"));

builder.Services.AddSingleton<TypesenseClientFactory>();
builder.Services.AddSingleton<IValkeyCache, ValkeyCache>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddScoped<ISuggestSearchClient, TypesenseSuggestSearchClient>();

builder.Services.AddSingleton<QuerySanitizer>();
builder.Services.AddSingleton<QueryNormalizer>();
builder.Services.AddSingleton<SynonymExpander>();
builder.Services.AddSingleton<IntentClassifier>();
builder.Services.AddSingleton<QueryPipeline>();
builder.Services.AddSingleton<SearchFilterExpressionValidator>();
builder.Services.AddSingleton<SearchRequestValidator>();

builder.Services.AddSingleton<RankingEngine>();

builder.Services.AddScoped<SearchQueryHandler>();
builder.Services.AddScoped<SuggestQueryHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "Nexora Search API", Version = "v1" }));

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
    if (builder.Environment.IsDevelopment() && (origins == null || origins.Contains("*")))
    {
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    }
    else
    {
        p.WithOrigins(origins ?? [])
         .AllowAnyMethod()
         .AllowAnyHeader()
         .AllowCredentials();
    }
}));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.Use(async (context, next) =>
{
    const string CorrelationIdHeader = "X-Correlation-ID";

    var correlationId =
        context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue) &&
        !string.IsNullOrWhiteSpace(headerValue)
            ? headerValue.ToString()
            : context.TraceIdentifier;

    context.TraceIdentifier = correlationId;
    context.Response.Headers[CorrelationIdHeader] = correlationId;

    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Nexora.SearchAPI.Request");

    using (logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId }))
    {
        logger.LogInformation("Handling {Method} {Path}", context.Request.Method, context.Request.Path);
        await next();
        logger.LogInformation(
            "Completed {Method} {Path} with {StatusCode}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode);
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();

// API Key authentication middleware for public endpoints (Phase 1.10)
// Skip in development environment if ApiKeys section is not configured
var apiKeysConfigured = app.Configuration.GetSection("ApiKeys:ValidKeys").Exists() ||
                        !string.IsNullOrWhiteSpace(app.Configuration["ApiKeys:ValidKeys:Csv"]);
if (apiKeysConfigured || !app.Environment.IsDevelopment())
{
    app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapSearchEndpoints();
app.MapSuggestEndpoints();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint("/metrics");

app.Run();

public partial class Program { }
