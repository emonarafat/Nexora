using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Nexora.SearchAPI.Features.Search;
using Nexora.SearchAPI.Features.Suggest;
using Nexora.SearchAPI.Infrastructure;
using Nexora.SearchAPI.Pipeline;
using Nexora.SearchAPI.Ranking;
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
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["Key"] ?? throw new InvalidOperationException("JWT Key not configured")))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
    o.AddSlidingWindowLimiter("SearchLimit", l =>
    {
        l.Window = TimeSpan.FromMinutes(1);
        l.SegmentsPerWindow = 6;
        l.PermitLimit = 1000;
        l.QueueLimit = 0;
    }));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Valkey") ?? "localhost:6379"));

builder.Services.AddSingleton<TypesenseClientFactory>();
builder.Services.AddSingleton<IValkeyCache, ValkeyCache>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

builder.Services.AddSingleton<QuerySanitizer>();
builder.Services.AddSingleton<QueryNormalizer>();
builder.Services.AddSingleton<SynonymExpander>();
builder.Services.AddSingleton<IntentClassifier>();
builder.Services.AddSingleton<QueryPipeline>();

builder.Services.AddSingleton<RankingEngine>();

builder.Services.AddScoped<SearchQueryHandler>();
builder.Services.AddScoped<SuggestQueryHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "Nexora Search API", Version = "v1" }));

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["*"])
     .AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapSearchEndpoints();
app.MapSuggestEndpoints();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint("/metrics");

app.Run();

public partial class Program { }
