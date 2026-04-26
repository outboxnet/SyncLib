using Microsoft.EntityFrameworkCore;
using SyncLib.Abstractions;

namespace SyncLib.EntityFrameworkCore;

/// <summary>
/// EF-Core backed <see cref="ISyncStateStore"/>. Persists per-provider sync
/// state into the table mapped by <see cref="SyncStateModelExtensions.ConfigureSyncState"/>.
/// </summary>
/// <typeparam name="TContext">A consumer <see cref="DbContext"/> implementing <see cref="ISyncStateDbContext"/>.</typeparam>
public sealed class EfSyncStateStore<TContext> : ISyncStateStore
    where TContext : DbContext, ISyncStateDbContext
{
    private readonly TContext _db;

    /// <inheritdoc />
    public EfSyncStateStore(TContext db) => _db = db;

    /// <inheritdoc />
    public async Task<SyncStateRecord?> GetAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var row = await _db.SyncStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderName == providerName, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToRecord(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<SyncStateRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.SyncStates.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ConvertAll(ToRecord);
    }

    /// <inheritdoc />
    public Task RecordRunStartedAsync(string providerName, DateTime startedAtUtc, CancellationToken cancellationToken = default) =>
        UpsertAsync(providerName, row =>
        {
            row.LastRunAt = startedAtUtc;
            row.LastStatus = (int)SyncStatus.Running;
        }, cancellationToken);

    /// <inheritdoc />
    public Task RecordSuccessAsync(string providerName, DateTime startedAtUtc, TimeSpan duration, int recordCount, CancellationToken cancellationToken = default) =>
        UpsertAsync(providerName, row =>
        {
            row.LastSuccessAt = startedAtUtc;
            row.LastRunAt = startedAtUtc + duration;
            row.LastStatus = (int)SyncStatus.Succeeded;
            row.LastDurationTicks = duration.Ticks;
            row.LastRecordCount = recordCount;
            row.LastError = null;
            row.TotalSuccesses += 1;
            row.ConsecutiveFailures = 0;
        }, cancellationToken);

    /// <inheritdoc />
    public Task RecordFailureAsync(string providerName, DateTime startedAtUtc, TimeSpan duration, Exception exception, CancellationToken cancellationToken = default) =>
        UpsertAsync(providerName, row =>
        {
            row.LastRunAt = startedAtUtc + duration;
            row.LastStatus = (int)SyncStatus.Failed;
            row.LastDurationTicks = duration.Ticks;
            row.LastError = Truncate(exception.Message, 4000);
            row.TotalFailures += 1;
            row.ConsecutiveFailures += 1;
        }, cancellationToken);

    /// <inheritdoc />
    public Task RecordSkippedAsync(string providerName, DateTime atUtc, string reason, CancellationToken cancellationToken = default) =>
        UpsertAsync(providerName, row =>
        {
            row.LastRunAt = atUtc;
            row.LastStatus = (int)SyncStatus.Skipped;
            row.LastError = Truncate(reason, 4000);
        }, cancellationToken);

    private async Task UpsertAsync(string providerName, Action<SyncStateEntity> mutate, CancellationToken cancellationToken)
    {
        var row = await _db.SyncStates.FirstOrDefaultAsync(x => x.ProviderName == providerName, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new SyncStateEntity { ProviderName = providerName };
            mutate(row);
            _db.SyncStates.Add(row);
        }
        else
        {
            mutate(row);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another writer updated the row first. Reload and reapply once.
            var entry = _db.Entry(row);
            await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
            mutate(row);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static SyncStateRecord ToRecord(SyncStateEntity row) => new()
    {
        ProviderName = row.ProviderName,
        LastSuccessAt = row.LastSuccessAt,
        LastRunAt = row.LastRunAt,
        LastStatus = (SyncStatus)row.LastStatus,
        LastDuration = row.LastDurationTicks is { } t ? TimeSpan.FromTicks(t) : null,
        LastRecordCount = row.LastRecordCount,
        LastError = row.LastError,
        TotalSuccesses = row.TotalSuccesses,
        TotalFailures = row.TotalFailures,
        ConsecutiveFailures = row.ConsecutiveFailures
    };

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
