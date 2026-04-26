using Microsoft.Extensions.DependencyInjection;
using SyncLib.Abstractions;

namespace SyncLib.Core;

/// <summary>
/// DI-resolved registration capturing the closed generic types of one sync
/// provider. Created by <c>SyncProviderBuilder</c>; consumed by <see cref="SyncOrchestrator"/>.
/// Consumers do not normally instantiate this type directly.
/// </summary>
public sealed class SyncJobRegistration
{
    public required string ProviderName { get; init; }
    public required Type DataType { get; init; }
    public required Type EntityType { get; init; }
    public required Func<IServiceProvider, CancellationToken, Task<int>> ExecuteAsync { get; init; }

    public static SyncJobRegistration Create<TData, TEntity>(string providerName)
        where TData : class
        where TEntity : class, IEntity
    {
        return new SyncJobRegistration
        {
            ProviderName = providerName,
            DataType = typeof(TData),
            EntityType = typeof(TEntity),
            ExecuteAsync = async (sp, ct) =>
            {
                var stateReader = sp.GetRequiredService<ISyncStateStore>();
                var dataProvider = sp.GetRequiredService<ISyncDataProvider<TData>>();
                var repository = sp.GetRequiredService<ISyncRepository<TEntity>>();
                var mapper = sp.GetRequiredService<ISyncMapper<TData, TEntity>>();

                var lastRun = await stateReader.GetAsync(providerName, ct).ConfigureAwait(false);
                var fetched = lastRun?.LastSuccessAt is { } since
                    ? await dataProvider.FetchDataAsync(since, ct).ConfigureAwait(false)
                    : await dataProvider.FetchDataAsync(ct).ConfigureAwait(false);

                var entities = mapper.MapToEntities(fetched);
                await repository.AddOrUpdateBatchAsync(entities, ct).ConfigureAwait(false);
                return entities.Count;
            }
        };
    }
}
