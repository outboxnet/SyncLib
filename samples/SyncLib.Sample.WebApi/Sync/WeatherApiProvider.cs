using SyncLib.Abstractions;

namespace SyncLib.Sample.WebApi.Sync;

/// <summary>
/// Stand-in for a real HTTP weather API. Generates deterministic data based on
/// the requested cities so the sample can run with no external dependencies.
/// </summary>
public sealed class WeatherApiProvider : ISyncDataProvider<WeatherDto>
{
    private static readonly string[] Cities = ["Stockholm", "Berlin", "Tokyo", "Toronto", "Cape Town"];

    public string ProviderName => "weather";

    public Task<IReadOnlyCollection<WeatherDto>> FetchDataAsync(CancellationToken cancellationToken = default) =>
        FetchDataAsync(null, cancellationToken);

    public Task<IReadOnlyCollection<WeatherDto>> FetchDataAsync(DateTime? lastSyncTime, CancellationToken cancellationToken = default)
    {
        var rng = Random.Shared;
        var observedAt = DateTime.UtcNow;
        var data = Cities.Select(c => new WeatherDto(
            City: c,
            TemperatureC: Math.Round(-5 + rng.NextDouble() * 35, 1),
            ObservedAt: observedAt)).ToArray();
        return Task.FromResult<IReadOnlyCollection<WeatherDto>>(data);
    }
}
