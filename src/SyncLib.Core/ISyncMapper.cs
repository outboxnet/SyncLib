using SyncLib.Abstractions;

namespace SyncLib.Core;

/// <summary>
/// Maps DTOs returned by a <see cref="ISyncDataProvider{TData}"/> to the
/// domain entity persisted by an <see cref="ISyncRepository{TEntity}"/>.
/// </summary>
public interface ISyncMapper<TData, TEntity>
    where TData : class
    where TEntity : class, IEntity
{
    /// <summary>Map a single DTO.</summary>
    TEntity MapToEntity(TData data);

    /// <summary>Map a batch of DTOs.</summary>
    IReadOnlyCollection<TEntity> MapToEntities(IEnumerable<TData> data);
}
