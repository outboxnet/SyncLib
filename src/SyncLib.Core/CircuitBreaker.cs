// Core/SyncOrchestrator.cs
using Microsoft.Extensions.Logging;

namespace SyncLibrary.Core;

// Core/CircuitBreaker.cs
internal class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    private readonly ILogger _logger;
    private int _failureCount;
    private DateTime? _openUntil;
    private readonly object _lock = new();

    public CircuitBreaker(int failureThreshold, TimeSpan timeout, ILogger logger)
    {
        _failureThreshold = failureThreshold;
        _timeout = timeout;
        _logger = logger;
    }

    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                if (!_openUntil.HasValue) return false;
                if (DateTime.UtcNow >= _openUntil.Value)
                {
                    _openUntil = null;
                    _failureCount = 0;
                    _logger.LogInformation("Circuit breaker closed after timeout");
                    return false;
                }
                return true;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _openUntil = null;
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            if (_failureCount >= _failureThreshold && !_openUntil.HasValue)
            {
                _openUntil = DateTime.UtcNow.Add(_timeout);
                _logger.LogWarning("Circuit breaker opened for {Timeout} due to {FailureCount} failures",
                    _timeout, _failureCount);
            }
        }
    }
}
