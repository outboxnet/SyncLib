using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SyncLib.Abstractions;
using SyncLib.Core.Configuration;
using SyncLib.Core.DependencyInjection;
using SyncLib.Sample.AzureFunctions.Sync;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Functions: register the runner ONLY (no hosted scheduler — the timer trigger
// is the scheduler). Same fluent provider registration as in the WebApi sample.
builder.Services.AddSyncRunner();

builder.Services.AddSyncProvider<WeatherDto, WeatherEntity>("weather")
    .WithConfiguration(new ProviderSyncConfiguration
    {
        ProviderName = "weather",
        // SyncInterval is unused in the Functions host — the [TimerTrigger] CRON drives cadence.
        SyncInterval = TimeSpan.FromMinutes(5),
        MaxRetryAttempts = 2,
        RetryDelayBase = TimeSpan.FromSeconds(1)
    })
    .WithDataProvider<WeatherApiProvider>()
    .WithRepository<MyWeatherRepository>()  // <-- your own implementation
    .WithMapper<WeatherMapper>()
    .Build();

builder.Build().Run();
