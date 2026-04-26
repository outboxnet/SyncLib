using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SyncLib.Abstractions;

namespace SyncLib.Core.DependencyInjection;

/// <summary>DI extensions for SyncLib.</summary>
public static class SyncServiceCollectionExtensions
{
    /// <summary>
    /// Register the SyncLib orchestrator and an in-memory sync state store. Replace
    /// the state store later (e.g. with <c>AddEntityFrameworkSyncStateStore</c>) for
    /// persistent state across restarts.
    /// </summary>
    public static IServiceCollection AddSyncLibrary(this IServiceCollection services)
    {
        services.TryAddSingleton<ISyncStateStore, InMemorySyncStateStore>();
        services.AddSingleton(sp => (ISyncStateReader)sp.GetRequiredService<ISyncStateStore>());

        services.AddSingleton<SyncOrchestrator>();
        services.AddSingleton<ISyncOrchestrator>(sp => sp.GetRequiredService<SyncOrchestrator>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SyncOrchestrator>());

        return services;
    }

    /// <summary>
    /// Begin building a sync provider registration. Call <c>WithConfiguration</c>,
    /// <c>WithDataProvider</c>, <c>WithRepository</c>, <c>WithMapper</c> and then
    /// <c>Build()</c>.
    /// </summary>
    public static ISyncProviderBuilder<TData, TEntity> AddSyncProvider<TData, TEntity>(
        this IServiceCollection services,
        string providerName)
        where TData : class
        where TEntity : class, IEntity
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name must be provided.", nameof(providerName));
        }
        return new SyncProviderBuilder<TData, TEntity>(services, providerName);
    }
}
