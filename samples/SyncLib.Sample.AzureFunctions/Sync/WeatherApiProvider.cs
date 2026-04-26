using SyncLib.Abstractions;

namespace SyncLib.Sample.AzureFunctions.Sync;

public sealed class WeatherApiProvider : ISyncDataProvider<WeatherDto>
{
    private static readonly string[] Cities = ["Stockholm", "Berlin", "Tokyo"];

    public string ProviderName => "weather";

    public Task<IReadOnlyCollection<WeatherDto>> FetchDataAsync(CancellationToken cancellationToken = default) =>
        FetchDataAsync(null, cancellationToken);

    public Task<IReadOnlyCollection<WeatherDto>> FetchDataAsync(DateTime? lastSyncTime, CancellationToken cancellationToken = default)
    {
        var rng = Random.Shared;
        var observedAt = DateTime.UtcNow;
        var data = Cities.Select(c => new WeatherDto(c, Math.Round(-5 + rng.NextDouble() * 35, 1), observedAt)).ToArray();
        return Task.FromResult<IReadOnlyCollection<WeatherDto>>(data);
    }
}
