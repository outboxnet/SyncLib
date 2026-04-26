// Core/ISyncMapper.cs
using SyncLibrary.Abstractions;

namespace SyncLibrary.Core;

public interface ISyncMapper<TData, TEntity>
    where TData : class
    where TEntity : class, IEntity
{
    TEntity MapToEntity(TData data);
    IEnumerable<TEntity> MapToEntities(IEnumerable<TData> data);
}