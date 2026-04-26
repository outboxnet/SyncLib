namespace SyncLib.Abstractions;

/// <summary>
/// Persistence for synchronized domain entities. Implementations are typically
/// thin wrappers over EF Core, Dapper, or any other data access layer.
/// </summary>
/// <remarks>
/// This interface intentionally does <b>not</b> expose last-sync metadata.
/// Sync state is tracked separately by <see cref="ISyncStateStore"/> so that
/// domain entities are not polluted with infrastructure concerns.
/// </remarks>
public interface ISyncRepository<TEntity> where TEntity : class, IEntity
{
    /// <summary>Insert new entities or update existing ones (matched by Id) in a single batch.</summary>
    Task AddOrUpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>Total number of locally persisted entities for diagnostics.</summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Delete entities whose <c>UpdatedAt</c> (or <c>CreatedAt</c> if null) is older than <paramref name="olderThan"/>.</summary>
    Task ClearOldDataAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
