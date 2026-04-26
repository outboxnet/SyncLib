// SyncAbstractions/ISyncConfiguration.cs
namespace SyncLibrary.Abstractions;

public interface ISyncConfiguration
{
    string ProviderName { get; }
    TimeSpan SyncInterval { get; }
    int MaxRetryAttempts { get; }
    TimeSpan RetryDelayBase { get; }
    bool EnableCircuitBreaker { get; }
    int FailureThreshold { get; }
    TimeSpan CircuitBreakerTimeout { get; }
}