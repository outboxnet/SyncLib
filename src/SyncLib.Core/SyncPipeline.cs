using Microsoft.Extensions.DependencyInjection;
using SyncLib.Abstractions;

namespace SyncLib.Core;

/// <summary>
/// The fetch → map → persist pipeline executed once per sync run for a single
/// provider. Resolves <see cref="ISyncDataProvider{TData}"/>,
/// <see cref="ISyncMapper{TData,TEntity}"/> and <see cref="ISyncRepository{TEntity}"/>
/// from the supplied scope and uses <see cref="ISyncStateStore"/> to look up
/// the last successful run for incremental fetches.
/// </summary>
/// <remarks>
/// Override <see cref="ExecuteAsync"/> in a derived class to customise the
/// pipeline for one provider (e.g. to inject validation, deduplicate, batch
/// the persist call, or use a non-time-based incremental key). Register the
/// derived pipeline by calling <see cref="SyncJobRegistration.Create{TData, TEntity}(SyncPipeline{TData, TEntity})"/>.
/// </remarks>
public class SyncPipeline<TData, TEntity>
    where TData : class
    where TEntity : class, IEntity
{
    /// <summary>The provider this pipeline serves.</summary>
    public string ProviderName { get; }

    /// <summary>Construct a pipeline bound to the given provider name.</summary>
    public SyncPipeline(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name must be provided.", nameof(providerName));
        }
        ProviderName = providerName;
    }

    /// <summary>
    /// Run the pipeline once and return the number of entities persisted.
    /// </summary>
    /// <param name="services">A scoped <see cref="IServiceProvider"/> created by the runner.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task<int> ExecuteAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var stateStore = services.GetRequiredService<ISyncStateStore>();
        var dataProvider = services.GetRequiredService<ISyncDataProvider<TData>>();
        var mapper = services.GetRequiredService<ISyncMapper<TData, TEntity>>();
        var repository = services.GetRequiredService<ISyncRepository<TEntity>>();

        var lastRun = await stateStore.GetAsync(ProviderName, cancellationToken).ConfigureAwait(false);
        var fetched = await FetchAsync(dataProvider, lastRun?.LastSuccessAt, cancellationToken).ConfigureAwait(false);
        var entities = mapper.MapToEntities(fetched);
        await repository.AddOrUpdateBatchAsync(entities, cancellationToken).ConfigureAwait(false);

        return entities.Count;
    }

    /// <summary>
    /// Fetch step. By default does an incremental fetch when the last
    /// successful run is known and a full fetch otherwise. Override to use a
    /// different incremental key (e.g. a sequence id) or to combine sources.
    /// </summary>
    protected virtual Task<IReadOnlyCollection<TData>> FetchAsync(
        ISyncDataProvider<TData> dataProvider,
        DateTime? lastSuccessAt,
        CancellationToken cancellationToken) =>
        lastSuccessAt is { } since
            ? dataProvider.FetchDataAsync(since, cancellationToken)
            : dataProvider.FetchDataAsync(cancellationToken);
}
