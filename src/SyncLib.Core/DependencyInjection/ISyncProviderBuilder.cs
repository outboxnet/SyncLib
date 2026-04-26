// DependencyInjection/ISyncProviderBuilder.cs (Improved Version)
using Microsoft.Extensions.DependencyInjection;
using SyncLibrary.Abstractions;
using SyncLibrary.Core;

namespace SyncLibrary.DependencyInjection;

public interface ISyncProviderBuilder<TData, TEntity>
    where TData : class
    where TEntity : class, IEntity
{
    ISyncProviderBuilder<TData, TEntity> WithConfiguration<TConfiguration>()
        where TConfiguration : class, ISyncConfiguration;

    ISyncProviderBuilder<TData, TEntity> WithDataProvider<TProvider>()
        where TProvider : class, ISyncDataProvider<TData>;

    ISyncProviderBuilder<TData, TEntity> WithRepository<TRepository>()
        where TRepository : class, ISyncRepository<TEntity>;

    ISyncProviderBuilder<TData, TEntity> WithMapper<TMapper>()
        where TMapper : class, ISyncMapper<TData, TEntity>;

    IServiceCollection Build();
}
