using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nexora.SearchAPI.Tests.Integration;

/// <summary>
/// Base class for integration tests that require a PostgreSQL database.
/// Uses Testcontainers to spin up an isolated PostgreSQL instance per test class.
/// </summary>
public abstract class PostgreSqlIntegrationTestBase : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;

    protected string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Build PostgreSQL container
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("nexora_test")
            .WithUsername("testuser")
            .WithPassword("testpass123")
            .WithCleanUp(true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        // Start container
        await _postgresContainer.StartAsync();

        // Get connection string
        ConnectionString = _postgresContainer.GetConnectionString();

        // Initialize database schema
        await InitializeDatabaseSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    /// <summary>
    /// Override this method to initialize database schema for your tests.
    /// </summary>
    protected virtual Task InitializeDatabaseSchemaAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper method to create the synonyms table required by Phase 1.
    /// </summary>
    protected async Task CreateSynonymsTableAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new Npgsql.NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS search_synonyms (
                id SERIAL PRIMARY KEY,
                term VARCHAR(255) NOT NULL,
                synonyms TEXT[] NOT NULL,
                locale VARCHAR(10) DEFAULT 'en-US',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                is_active BOOLEAN DEFAULT TRUE
            );

            CREATE INDEX IF NOT EXISTS idx_search_synonyms_term ON search_synonyms(term);
            CREATE INDEX IF NOT EXISTS idx_search_synonyms_locale ON search_synonyms(locale);
        ", conn);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Helper method to seed synonym data for testing.
    /// </summary>
    protected async Task SeedSynonymsAsync(params (string Term, string[] Synonyms)[] synonymData)
    {
        await using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        foreach (var (term, synonyms) in synonymData)
        {
            await using var cmd = new Npgsql.NpgsqlCommand(@"
                INSERT INTO search_synonyms (term, synonyms, locale, is_active)
                VALUES (@term, @synonyms, 'en-US', TRUE)
            ", conn);

            cmd.Parameters.AddWithValue("term", term);
            cmd.Parameters.AddWithValue("synonyms", synonyms);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
