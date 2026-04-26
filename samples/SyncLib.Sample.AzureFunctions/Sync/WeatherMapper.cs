using SyncLib.Core;

namespace SyncLib.Sample.AzureFunctions.Sync;

public sealed class WeatherMapper : ISyncMapper<WeatherDto, WeatherEntity>
{
    public WeatherEntity MapToEntity(WeatherDto data) => new()
    {
        Id = DeterministicGuid(data.City),
        City = data.City,
        TemperatureC = data.TemperatureC,
        ObservedAt = data.ObservedAt
    };

    public IReadOnlyCollection<WeatherEntity> MapToEntities(IEnumerable<WeatherDto> data) =>
        data.Select(MapToEntity).ToArray();

    private static Guid DeterministicGuid(string s)
    {
        var bytes = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(s));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        return new Guid(guid);
    }
}
