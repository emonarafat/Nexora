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
            var count = Math.Min(_batchSize, items.Count - index);
            var batch = new T[count];
            for (var batchIndex = 0; batchIndex < count; batchIndex++)
                batch[batchIndex] = items[index + batchIndex];

            yield return batch;
        }
    }
}
