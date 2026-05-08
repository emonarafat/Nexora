using Nexora.AdminAPI.Features.ReIndex;
using Nexora.AdminAPI.Features.Synonyms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "Nexora Admin API", Version = "v1" }));
builder.Services.AddHttpClient("IndexSync", c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddNpgsqlDataSource(
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Postgres connection string not configured"));
builder.Services.AddHealthChecks();

var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseAuthorization();
app.MapSynonymEndpoints();
app.MapReIndexEndpoints();
app.MapHealthChecks("/health");
app.Run();
