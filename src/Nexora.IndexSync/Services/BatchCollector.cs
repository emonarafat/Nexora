using Microsoft.Extensions.Options;
using Nexora.IndexSync.Options;

namespace Nexora.IndexSync.Services;

public sealed class BatchCollector(IOptions<IndexSyncOptions> options)
{
    private readonly int _batchSize = Math.Max(1, options.Value.BatchSize);

    public IEnumerable<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T> items)
    {
        for (var index = 0; index < items.Count; index += _batchSize)
        {
            yield return items.Skip(index).Take(_batchSize).ToArray();
        }
    }
}
