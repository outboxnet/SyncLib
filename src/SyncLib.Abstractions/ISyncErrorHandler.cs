// SyncAbstractions/ISyncErrorHandler.cs
namespace SyncLibrary.Abstractions;

public interface ISyncErrorHandler
{
    Task OnSyncErrorAsync(string providerName, Exception exception, int retryCount, CancellationToken cancellationToken = default);
    Task OnSyncSuccessAsync(string providerName, int recordsSynced, TimeSpan duration, CancellationToken cancellationToken = default);
}
