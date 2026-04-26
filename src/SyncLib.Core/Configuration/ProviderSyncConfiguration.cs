using SyncLib.Abstractions;

namespace SyncLib.Core.Configuration;

/// <summary>
/// Default <see cref="ISyncConfiguration"/> implementation. Bind from
/// <c>IConfiguration</c> or construct inline when registering a provider.
/// </summary>
public sealed class ProviderSyncConfiguration : ISyncConfiguration
{
    /// <inheritdoc />
    public string ProviderName { get; set; } = null!;

    /// <inheritdoc />
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public int MaxRetryAttempts { get; set; } = 3;

    /// <inheritdoc />
    public TimeSpan RetryDelayBase { get; set; } = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <inheritdoc />
    public int FailureThreshold { get; set; } = 5;

    /// <inheritdoc />
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
