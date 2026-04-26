using SyncLib.Abstractions;

namespace SyncLib.Core;

/// <summary>
/// DI-resolved registration capturing the closed generic types of one sync
/// provider. Created by <c>SyncProviderBuilder</c>; consumed by <see cref="SyncRunner"/>.
/// Consumers do not normally instantiate this type directly — call
/// <c>services.AddSyncProvider&lt;TData, TEntity&gt;(name)</c> on the builder.
/// </summary>
public sealed class SyncJobRegistration
{
    /// <summary>Provider name; matches the corresponding <see cref="ISyncConfiguration"/>.</summary>
    public required string ProviderName { get; init; }

    /// <summary>The DTO type produced by <see cref="ISyncDataProvider{TData}"/>.</summary>
    public required Type DataType { get; init; }

    /// <summary>The domain entity type persisted by <see cref="ISyncRepository{TEntity}"/>.</summary>
    public required Type EntityType { get; init; }

    /// <summary>
    /// Executes one run of the pipeline against the supplied scope and returns
    /// the number of entities persisted. The runner adds retry, breaker, state
    /// recording and metrics around this call.
    /// </summary>
    public required Func<IServiceProvider, CancellationToken, Task<int>> ExecuteAsync { get; init; }

    /// <summary>
    /// Build a registration that uses the default <see cref="SyncPipeline{TData, TEntity}"/>.
    /// </summary>
    public static SyncJobRegistration Create<TData, TEntity>(string providerName)
        where TData : class
        where TEntity : class, IEntity
        => Create(new SyncPipeline<TData, TEntity>(providerName));

    /// <summary>
    /// Build a registration around a custom (typically derived) pipeline. Use
    /// this when one provider needs a non-default fetch strategy or extra
    /// pipeline stages.
    /// </summary>
    public static SyncJobRegistration Create<TData, TEntity>(SyncPipeline<TData, TEntity> pipeline)
        where TData : class
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return new SyncJobRegistration
        {
            ProviderName = pipeline.ProviderName,
            DataType = typeof(TData),
            EntityType = typeof(TEntity),
            ExecuteAsync = pipeline.ExecuteAsync
        };
    }
}
