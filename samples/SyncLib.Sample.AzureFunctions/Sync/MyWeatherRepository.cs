using Microsoft.Extensions.Logging;
using SyncLib.Abstractions;

namespace SyncLib.Sample.AzureFunctions.Sync;

/// <summary>
/// Caller-supplied repository — this is "your own implementation" of how the
/// data is persisted. SyncLib calls <see cref="AddOrUpdateBatchAsync"/> on every
/// run with the mapped entities; the rest of the pipeline (state, metrics,
/// retries, breaker) is handled by the framework.
/// </summary>
public sealed class MyWeatherRepository : ISyncRepository<WeatherEntity>
{
    private readonly ILogger<MyWeatherRepository> _logger;

    public MyWeatherRepository(ILogger<MyWeatherRepository> logger) => _logger = logger;

    public Task AddOrUpdateBatchAsync(IEnumerable<WeatherEntity> entities, CancellationToken cancellationToken = default)
    {
        // Replace this body with your real persistence — Dapper, EF Core,
        // Cosmos, an HTTP upstream, anything. This is the only place the
        // sample diverges from the WebApi sample.
        var count = 0;
        foreach (var e in entities)
        {
            count++;
            _logger.LogInformation("Persisting {City} = {Temp:F1} °C @ {Observed:o}",
                e.City, e.TemperatureC, e.ObservedAt);
        }
        _logger.LogInformation("Upserted {Count} weather rows.", count);
        return Task.CompletedTask;
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task ClearOldDataAsync(DateTime olderThan, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
