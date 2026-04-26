namespace SyncLib.Abstractions;

/// <summary>
/// Source of data for one provider. Implementations typically wrap an HTTP client,
/// a queue consumer, or a file reader.
/// </summary>
public interface ISyncDataProvider<TData> where TData : class
{
    /// <summary>The provider's unique name. Must match <see cref="ISyncConfiguration.ProviderName"/>.</summary>
    string ProviderName { get; }

    /// <summary>Fetch all data (initial load).</summary>
    Task<IReadOnlyCollection<TData>> FetchDataAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetch incremental data since <paramref name="lastSyncTime"/> (UTC).</summary>
    Task<IReadOnlyCollection<TData>> FetchDataAsync(DateTime? lastSyncTime, CancellationToken cancellationToken = default);
}
