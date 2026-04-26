// SyncAbstractions/ISyncRepository.cs
namespace SyncLibrary.Abstractions;

public interface ISyncRepository<TEntity> where TEntity : class, IEntity
{
    Task AddOrUpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    Task ClearOldDataAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
