// Core/SyncOrchestrator.cs
namespace SyncLibrary.Core;

public interface ISyncOrchestrator
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task TriggerManualSyncAsync(string providerName, CancellationToken cancellationToken = default);
}
