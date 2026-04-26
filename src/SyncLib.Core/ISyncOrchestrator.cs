namespace SyncLib.Core;

/// <summary>
/// Hosted service that runs registered sync providers on a schedule. Resolved
/// from DI as a singleton; also registered as <see cref="Microsoft.Extensions.Hosting.IHostedService"/>.
/// </summary>
public interface ISyncOrchestrator
{
    /// <summary>Trigger a single run of <paramref name="providerName"/> immediately, outside the schedule.</summary>
    /// <exception cref="ArgumentException">No provider with this name is registered.</exception>
    Task TriggerManualSyncAsync(string providerName, CancellationToken cancellationToken = default);

    /// <summary>Names of all registered providers.</summary>
    IReadOnlyCollection<string> RegisteredProviders { get; }
}
