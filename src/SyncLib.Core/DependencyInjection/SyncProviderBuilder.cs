using Microsoft.Extensions.DependencyInjection;
using SyncLib.Abstractions;

namespace SyncLib.Core.DependencyInjection;

internal sealed class SyncProviderBuilder<TData, TEntity> : ISyncProviderBuilder<TData, TEntity>
    where TData : class
    where TEntity : class, IEntity
{
    private readonly IServiceCollection _services;
    private readonly string _providerName;
    private bool _hasConfiguration;
    private bool _hasDataProvider;
    private bool _hasRepository;
    private bool _hasMapper;

    public SyncProviderBuilder(IServiceCollection services, string providerName)
    {
        _services = services;
        _providerName = providerName;
    }

    public ISyncProviderBuilder<TData, TEntity> WithConfiguration(ISyncConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!string.Equals(configuration.ProviderName, _providerName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Configuration ProviderName '{configuration.ProviderName}' does not match registered provider name '{_providerName}'.");
        }
        _services.AddSingleton(configuration);
        _hasConfiguration = true;
        return this;
    }

    public ISyncProviderBuilder<TData, TEntity> WithConfiguration<TConfiguration>()
        where TConfiguration : class, ISyncConfiguration
    {
        _services.AddSingleton<ISyncConfiguration, TConfiguration>();
        _hasConfiguration = true;
        return this;
    }

    public ISyncProviderBuilder<TData, TEntity> WithDataProvider<TProvider>()
        where TProvider : class, ISyncDataProvider<TData>
    {
        _services.AddScoped<ISyncDataProvider<TData>, TProvider>();
        _hasDataProvider = true;
        return this;
    }

    public ISyncProviderBuilder<TData, TEntity> WithRepository<TRepository>()
        where TRepository : class, ISyncRepository<TEntity>
    {
        _services.AddScoped<ISyncRepository<TEntity>, TRepository>();
        _hasRepository = true;
        return this;
    }

    public ISyncProviderBuilder<TData, TEntity> WithMapper<TMapper>()
        where TMapper : class, ISyncMapper<TData, TEntity>
    {
        _services.AddScoped<ISyncMapper<TData, TEntity>, TMapper>();
        _hasMapper = true;
        return this;
    }

    public IServiceCollection Build()
    {
        if (!_hasConfiguration) throw new InvalidOperationException("WithConfiguration(...) must be called.");
        if (!_hasDataProvider) throw new InvalidOperationException("WithDataProvider<TProvider>() must be called.");
        if (!_hasRepository) throw new InvalidOperationException("WithRepository<TRepository>() must be called.");
        if (!_hasMapper) throw new InvalidOperationException("WithMapper<TMapper>() must be called.");

        _services.AddSingleton(SyncJobRegistration.Create<TData, TEntity>(_providerName));
        return _services;
    }
}
