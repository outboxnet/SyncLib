// SyncAbstractions/ISyncDataProvider.cs
namespace SyncLibrary.Abstractions;

public interface ISyncDataProvider<TData> where TData : class
{
    string ProviderName { get; }
    Task<IEnumerable<TData>> FetchDataAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<TData>> FetchDataAsync(DateTime? lastSyncTime, CancellationToken cancellationToken = default);
}
