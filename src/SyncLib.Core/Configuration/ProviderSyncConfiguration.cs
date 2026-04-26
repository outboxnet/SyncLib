// Configuration/ProviderSyncConfiguration.cs
using SyncLibrary.Abstractions;

namespace SyncLibrary.Configuration;

public class ProviderSyncConfiguration : ISyncConfiguration
{
    public string ProviderName { get; set; } = null!;
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelayBase { get; set; } = TimeSpan.FromSeconds(2);
    public bool EnableCircuitBreaker { get; set; } = true;
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(5);
}