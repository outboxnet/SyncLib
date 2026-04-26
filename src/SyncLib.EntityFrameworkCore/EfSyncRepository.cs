using Microsoft.EntityFrameworkCore;
using SyncLib.Abstractions;

namespace SyncLib.EntityFrameworkCore;

/// <summary>
/// Generic EF-Core implementation of <see cref="ISyncRepository{TEntity}"/>. Performs
/// upsert by primary key (<see cref="IEntity.Id"/>) and stamps timestamps.
/// </summary>
/// <typeparam name="TContext">Consumer <see cref="DbContext"/>.</typeparam>
/// <typeparam name="TEntity">Entity to persist; must implement <see cref="IEntity"/>.</typeparam>
public class EfSyncRepository<TContext, TEntity> : ISyncRepository<TEntity>
    where TContext : DbContext
    where TEntity : class, IEntity
{
    /// <summary>The injected DbContext. Available to subclasses that want to add custom queries.</summary>
    protected TContext Context { get; }

    /// <summary>The DbSet for this entity.</summary>
    protected DbSet<TEntity> Set => Context.Set<TEntity>();

    /// <inheritdoc />
    public EfSyncRepository(TContext context) => Context = context;

    /// <inheritdoc />
    public virtual async Task AddOrUpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var incoming = entities as IList<TEntity> ?? entities.ToList();
        if (incoming.Count == 0)
        {
            return;
        }

        var ids = incoming.Select(e => e.Id).ToArray();
        var existing = await Set.Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        foreach (var entity in incoming)
        {
            if (existing.TryGetValue(entity.Id, out var current))
            {
                Context.Entry(current).CurrentValues.SetValues(entity);
                current.UpdatedAt = now;
            }
            else
            {
                if (entity.CreatedAt == default)
                {
                    entity.CreatedAt = now;
                }
                entity.UpdatedAt = null;
                Set.Add(entity);
            }
        }

        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual Task<int> GetCountAsync(CancellationToken cancellationToken = default) =>
        Set.CountAsync(cancellationToken);

    /// <inheritdoc />
    public virtual async Task ClearOldDataAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        var rows = await Set.Where(e => (e.UpdatedAt ?? e.CreatedAt) < olderThan).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return;
        }
        Set.RemoveRange(rows);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
