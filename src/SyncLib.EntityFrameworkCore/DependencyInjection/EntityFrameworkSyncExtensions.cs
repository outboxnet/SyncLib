using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SyncLib.Abstractions;

namespace SyncLib.EntityFrameworkCore;

/// <summary>DI helpers for the EF-Core integration.</summary>
public static class EntityFrameworkSyncExtensions
{
    /// <summary>
    /// Replace the in-memory state store with an EF-Core backed one. Your
    /// <typeparamref name="TContext"/> must implement <see cref="ISyncStateDbContext"/>
    /// and call <c>modelBuilder.ConfigureSyncState()</c> in <c>OnModelCreating</c>.
    /// </summary>
    public static IServiceCollection AddEntityFrameworkSyncStateStore<TContext>(this IServiceCollection services)
        where TContext : DbContext, ISyncStateDbContext
    {
        services.AddScoped<ISyncStateStore, EfSyncStateStore<TContext>>();
        return services;
    }

    /// <summary>
    /// Register a default <see cref="EfSyncRepository{TContext, TEntity}"/> as
    /// <see cref="ISyncRepository{TEntity}"/>. Use this from inside the sync
    /// provider builder via <c>WithRepository&lt;EfSyncRepository&lt;TContext, TEntity&gt;&gt;()</c>
    /// or call this directly when a custom repository isn't needed.
    /// </summary>
    public static IServiceCollection AddEntityFrameworkSyncRepository<TContext, TEntity>(this IServiceCollection services)
        where TContext : DbContext
        where TEntity : class, IEntity
    {
        services.TryAddScoped<ISyncRepository<TEntity>, EfSyncRepository<TContext, TEntity>>();
        return services;
    }
}
