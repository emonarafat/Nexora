using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.IndexSync.Services;

namespace Nexora.IndexSync.Tests.Services;

public class SyncDeadLetterWriterTests
{
    [Fact]
    public async Task WriteAsync_EmptyChanges_ReturnsImmediately()
    {
        // Arrange: empty change list – should return without touching the DB
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var writer = new SyncDeadLetterWriter(config, NullLogger<SyncDeadLetterWriter>.Instance);

        // Act + Assert: must NOT throw even though DB is unavailable
        var act = async () => await writer.WriteAsync([], "some error", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
