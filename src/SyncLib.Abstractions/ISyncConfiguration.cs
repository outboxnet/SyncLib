namespace SyncLib.Abstractions;

/// <summary>
/// Per-provider configuration controlling schedule, retries and circuit-breaker behaviour.
/// </summary>
public interface ISyncConfiguration
{
    /// <summary>Unique provider name. Used as a key in metrics, tracing, and the state store.</summary>
    string ProviderName { get; }

    /// <summary>How often the orchestrator runs this provider's sync.</summary>
    TimeSpan SyncInterval { get; }

    /// <summary>Maximum number of retries on a single failed run before the run is abandoned.</summary>
    int MaxRetryAttempts { get; }

    /// <summary>Base delay used for exponential back-off between retries.</summary>
    TimeSpan RetryDelayBase { get; }

    /// <summary>When true, repeated failures will open a circuit breaker for this provider.</summary>
    bool EnableCircuitBreaker { get; }

    /// <summary>Number of consecutive failed runs that trips the breaker.</summary>
    int FailureThreshold { get; }

    /// <summary>How long the breaker stays open before it half-closes.</summary>
    TimeSpan CircuitBreakerTimeout { get; }
}
