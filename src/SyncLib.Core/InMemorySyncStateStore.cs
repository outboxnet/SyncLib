using System.Collections.Concurrent;
using SyncLib.Abstractions;

namespace SyncLib.Core;

/// <summary>
/// Process-local <see cref="ISyncStateStore"/> for tests, samples and apps that do
/// not need persistence. State is lost on restart — use <c>SyncLib.EntityFrameworkCore</c>
/// for production deployments.
/// </summary>
public sealed class InMemorySyncStateStore : ISyncStateStore
{
    private readonly ConcurrentDictionary<string, SyncStateRecord> _state = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<SyncStateRecord?> GetAsync(string providerName, CancellationToken cancellationToken = default)
    {
        _state.TryGetValue(providerName, out var record);
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<SyncStateRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<SyncStateRecord>>(_state.Values.ToArray());

    /// <inheritdoc />
    public Task RecordRunStartedAsync(string providerName, DateTime startedAtUtc, CancellationToken cancellationToken = default)
    {
        _state.AddOrUpdate(providerName,
            _ => new SyncStateRecord { ProviderName = providerName, LastStatus = SyncStatus.Running, LastRunAt = startedAtUtc },
            (_, prev) => prev with { LastStatus = SyncStatus.Running, LastRunAt = startedAtUtc });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordSuccessAsync(string providerName, DateTime startedAtUtc, TimeSpan duration, int recordCount, CancellationToken cancellationToken = default)
    {
        _state.AddOrUpdate(providerName,
            _ => new SyncStateRecord
            {
                ProviderName = providerName,
                LastStatus = SyncStatus.Succeeded,
                LastRunAt = startedAtUtc + duration,
                LastSuccessAt = startedAtUtc,
                LastDuration = duration,
                LastRecordCount = recordCount,
                TotalSuccesses = 1
            },
            (_, prev) => prev with
            {
                LastStatus = SyncStatus.Succeeded,
                LastRunAt = startedAtUtc + duration,
                LastSuccessAt = startedAtUtc,
                LastDuration = duration,
                LastRecordCount = recordCount,
                LastError = null,
                TotalSuccesses = prev.TotalSuccesses + 1,
                ConsecutiveFailures = 0
            });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordFailureAsync(string providerName, DateTime startedAtUtc, TimeSpan duration, Exception exception, CancellationToken cancellationToken = default)
    {
        _state.AddOrUpdate(providerName,
            _ => new SyncStateRecord
            {
                ProviderName = providerName,
                LastStatus = SyncStatus.Failed,
                LastRunAt = startedAtUtc + duration,
                LastDuration = duration,
                LastError = exception.Message,
                TotalFailures = 1,
                ConsecutiveFailures = 1
            },
            (_, prev) => prev with
            {
                LastStatus = SyncStatus.Failed,
                LastRunAt = startedAtUtc + duration,
                LastDuration = duration,
                LastError = exception.Message,
                TotalFailures = prev.TotalFailures + 1,
                ConsecutiveFailures = prev.ConsecutiveFailures + 1
            });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordSkippedAsync(string providerName, DateTime atUtc, string reason, CancellationToken cancellationToken = default)
    {
        _state.AddOrUpdate(providerName,
            _ => new SyncStateRecord { ProviderName = providerName, LastStatus = SyncStatus.Skipped, LastRunAt = atUtc, LastError = reason },
            (_, prev) => prev with { LastStatus = SyncStatus.Skipped, LastRunAt = atUtc, LastError = reason });
        return Task.CompletedTask;
    }
}
