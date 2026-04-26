using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SyncLib.Abstractions;

namespace SyncLib.Core.DependencyInjection;

/// <summary>DI extensions for SyncLib.</summary>
public static class SyncServiceCollectionExtensions
{
    /// <summary>
    /// Register the on-demand <see cref="ISyncRunner"/> and an in-memory state store.
    /// Does <b>not</b> register a <see cref="IHostedService"/>. Use this from
    /// hosts that drive scheduling externally (Azure Functions timer trigger,
    /// console job, controller endpoint, …).
    /// </summary>
    public static IServiceCollection AddSyncRunner(this IServiceCollection services)
    {
        services.TryAddSingleton<ISyncStateStore, InMemorySyncStateStore>();
        services.TryAddSingleton(sp => (ISyncStateReader)sp.GetRequiredService<ISyncStateStore>());
        services.TryAddSingleton<ISyncRunner, SyncRunner>();
        return services;
    }

    /// <summary>
    /// Register everything <see cref="AddSyncRunner"/> registers, plus the hosted
    /// <see cref="SyncOrchestrator"/> that schedules each provider on its
    /// configured interval. Use from long-lived hosts (ASP.NET, Worker Service).
    /// </summary>
    public static IServiceCollection AddSyncLibrary(this IServiceCollection services)
    {
        services.AddSyncRunner();

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
