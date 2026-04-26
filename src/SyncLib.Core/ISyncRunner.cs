namespace SyncLib.Core;

/// <summary>
/// Executes a single sync run for one or all registered providers, applying
/// the same pipeline used by <see cref="SyncOrchestrator"/>: fetch via
/// <c>ISyncDataProvider</c>, map via <c>ISyncMapper</c>, persist via the
/// consumer's <c>ISyncRepository</c>, and record state/metrics. Use this from
/// hosts that drive scheduling externally — Azure Functions timer triggers,
/// console jobs, controllers, etc.
/// </summary>
public interface ISyncRunner
{
    /// <summary>Names of all registered providers.</summary>
    IReadOnlyCollection<string> RegisteredProviders { get; }

    /// <summary>Run one provider's sync once.</summary>
    /// <exception cref="ArgumentException">No provider with this name is registered.</exception>
    Task RunAsync(string providerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Run every registered provider's sync once, sequentially. Returns a
    /// summary of outcomes; an exception in one provider does not abort the
    /// others — failures are recorded and surfaced via the summary.
    /// </summary>
    Task<SyncRunSummary> RunAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Aggregate outcome of <see cref="ISyncRunner.RunAllAsync"/>.</summary>
public sealed record SyncRunSummary(
    int TotalProviders,
    int Succeeded,
    int Failed,
    int Skipped,
    IReadOnlyDictionary<string, string> ProviderErrors)
{
    /// <summary>True when every provider either succeeded or was skipped (no failures).</summary>
    public bool IsHealthy => Failed == 0;
}
