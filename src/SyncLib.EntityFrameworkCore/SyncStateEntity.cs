namespace SyncLib.EntityFrameworkCore;

/// <summary>
/// EF-mapped persistence row for sync state. Public so consumers can include it
/// in their <see cref="Microsoft.EntityFrameworkCore.DbContext"/>; only the
/// <see cref="EfSyncStateStore{TContext}"/> should mutate instances.
/// </summary>
public sealed class SyncStateEntity
{
    /// <summary>Provider name; primary key.</summary>
    public string ProviderName { get; set; } = null!;

    /// <summary>UTC time of the last successful run.</summary>
    public DateTime? LastSuccessAt { get; set; }

    /// <summary>UTC time of the most recent run completion.</summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>Most recent status (cast of <see cref="SyncLib.Abstractions.SyncStatus"/>).</summary>
    public int LastStatus { get; set; }

    /// <summary>Most recent duration in <see cref="TimeSpan"/> ticks.</summary>
    public long? LastDurationTicks { get; set; }

    /// <summary>Records persisted on the most recent successful run.</summary>
    public int? LastRecordCount { get; set; }

    /// <summary>Most recent error message, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>Lifetime count of successful runs.</summary>
    public long TotalSuccesses { get; set; }

    /// <summary>Lifetime count of failed runs.</summary>
    public long TotalFailures { get; set; }

    /// <summary>Number of consecutive failed runs (resets on success).</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>EF concurrency token.</summary>
    public byte[]? RowVersion { get; set; }
}
