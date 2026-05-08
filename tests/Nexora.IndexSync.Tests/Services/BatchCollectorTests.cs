using FluentAssertions;
using Microsoft.Extensions.Options;
using Nexora.IndexSync.Options;
using Nexora.IndexSync.Services;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Nexora.IndexSync.Tests.Services;

public class BatchCollectorTests
{
    [Fact]
    public void Chunk_SplitsChangesUsingConfiguredBatchSize()
    {
        var collector = new BatchCollector(OptionsFactory.Create(new IndexSyncOptions { BatchSize = 2 }));

        var chunks = collector.Chunk([1, 2, 3, 4, 5]).ToArray();

        chunks.Should().HaveCount(3);
        chunks[0].Should().Equal(1, 2);
        chunks[1].Should().Equal(3, 4);
        chunks[2].Should().Equal(5);
    }
}
