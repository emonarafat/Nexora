using Microsoft.AspNetCore.RateLimiting;
using Nexora.AdminAPI.Features.ReIndex;
using Nexora.AdminAPI.Features.Synonyms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "Nexora Admin API", Version = "v1" }));
builder.Services.AddNpgsqlDataSource(
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Postgres connection string not configured"));

// Phase 1.10: Rate limiting for admin endpoints
builder.Services.AddRateLimiter(o =>
    o.AddSlidingWindowLimiter("AdminLimit", l =>
    {
        l.Window = TimeSpan.FromMinutes(1);
        l.SegmentsPerWindow = 6;
        l.PermitLimit = 30;
        l.QueueLimit = 0;
    }));

builder.Services.AddHealthChecks();

var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseAuthorization();
app.UseRateLimiter();
app.MapSynonymEndpoints();
app.MapReIndexEndpoints();
app.MapHealthChecks("/health");
app.Run();
