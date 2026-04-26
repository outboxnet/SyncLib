// DependencyInjection/SyncProviderBuilder.cs (Improved Version)
using Microsoft.Extensions.DependencyInjection;
using SyncLibrary.Abstractions;
using SyncLibrary.Core;

namespace SyncLibrary.DependencyInjection;

internal class SyncProviderBuilder<TData, TEntity> : ISyncProviderBuilder<TData, TEntity>
    where TData : class
    where TEntity : class, IEntity
{
    private readonly IServiceCollection _services;
    private Type? _configurationType;
    private Type? _dataProviderType;
    private Type? _repositoryType;
    private Type? _mapperType;

    public SyncProviderBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public ISyncProviderBuilder<TData, TEntity> WithConfiguration<TConfiguration>()
        where TConfiguration : class, ISyncConfiguration
    {
        _configurationType = typeof(TConfiguration);
        _services.AddSingleton<ISyncConfiguration, TConfiguration>();
        return this;
    }

    public ISyncProviderBuilder<TData, TEntity> WithDataProvider<TProvider>()
        where TProvider : class, ISyncDataProvider<TData>
    {
        _dataProviderType = typeof(TProvider);
        _services.AddScoped<ISyncDataProvider<TData>, TProvider>();
        return this;
    }

    public ISyncProviderBuilder<TData, TEntity> WithRepository<TRepository>()
        where TRepository : class, ISyncRepository<TEntity>
    {
        _repositoryType = typeof(TRepository);
        _services.AddScoped<ISyncRepository<TEntity>, TRepository>();
        return this;
    }

    public ISyncProviderBuilder<TData, TEntity> WithMapper<TMapper>()
        where TMapper : class, ISyncMapper<TData, TEntity>
    {
        _mapperType = typeof(TMapper);
        _services.AddScoped<ISyncMapper<TData, TEntity>, TMapper>();
        return this;
    }

    public IServiceCollection Build()
    {
        if (_configurationType == null)
            throw new InvalidOperationException("Configuration must be specified. Call WithConfiguration<TConfiguration>()");

        if (_dataProviderType == null)
            throw new InvalidOperationException("Data provider must be specified. Call WithDataProvider<TProvider>()");

        if (_repositoryType == null)
            throw new InvalidOperationException("Repository must be specified. Call WithRepository<TRepository>()");

        if (_mapperType == null)
            throw new InvalidOperationException("Mapper must be specified. Call WithMapper<TMapper>()");

        return _services;
    }
}