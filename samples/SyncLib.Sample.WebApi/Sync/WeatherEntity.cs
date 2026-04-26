using SyncLib.Abstractions;

namespace SyncLib.Sample.WebApi.Sync;

public sealed class WeatherEntity : IEntity
{
    public Guid Id { get; set; }
    public string City { get; set; } = null!;
    public double TemperatureC { get; set; }
    public DateTime ObservedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
