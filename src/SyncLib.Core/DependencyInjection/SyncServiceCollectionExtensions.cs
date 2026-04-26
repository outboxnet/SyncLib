// DependencyInjection/SyncServiceCollectionExtensions.cs (Improved Version)
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SyncLibrary.Abstractions;
using SyncLibrary.Core;

namespace SyncLibrary.DependencyInjection;

public static class SyncServiceCollectionExtensions
{
    public static IServiceCollection AddSyncLibrary(this IServiceCollection services)
    {
        services.AddSingleton<SyncOrchestrator>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SyncOrchestrator>());
        services.AddSingleton<ISyncOrchestrator>(sp => sp.GetRequiredService<SyncOrchestrator>());

        return services;
    }

    public static ISyncProviderBuilder<TData, TEntity> AddSyncProvider<TData, TEntity>(
        this IServiceCollection services)
        where TData : class
        where TEntity : class, IEntity
    {
        return new SyncProviderBuilder<TData, TEntity>(services);
    }
}
