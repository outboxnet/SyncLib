using Microsoft.Extensions.DependencyInjection;
using SyncLib.Abstractions;

namespace SyncLib.Core.DependencyInjection;

/// <summary>
/// Fluent builder for registering one sync provider. Use
/// <see cref="SyncServiceCollectionExtensions.AddSyncProvider{TData,TEntity}"/> to obtain one.
/// </summary>
public interface ISyncProviderBuilder<TData, TEntity>
    where TData : class
    where TEntity : class, IEntity
{
    /// <summary>Register a configuration instance for this provider.</summary>
    ISyncProviderBuilder<TData, TEntity> WithConfiguration(ISyncConfiguration configuration);

    /// <summary>Register a configuration type resolved from DI as a singleton.</summary>
    ISyncProviderBuilder<TData, TEntity> WithConfiguration<TConfiguration>()
        where TConfiguration : class, ISyncConfiguration;

    /// <summary>Register the data provider (scoped) for this sync.</summary>
    ISyncProviderBuilder<TData, TEntity> WithDataProvider<TProvider>()
        where TProvider : class, ISyncDataProvider<TData>;

    /// <summary>Register the repository (scoped) for this sync.</summary>
    ISyncProviderBuilder<TData, TEntity> WithRepository<TRepository>()
        where TRepository : class, ISyncRepository<TEntity>;

    /// <summary>Register the mapper (scoped) for this sync.</summary>
    ISyncProviderBuilder<TData, TEntity> WithMapper<TMapper>()
        where TMapper : class, ISyncMapper<TData, TEntity>;

    /// <summary>Validate the configuration and complete provider registration.</summary>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    IServiceCollection Build();
}
