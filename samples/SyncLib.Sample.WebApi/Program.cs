using Microsoft.EntityFrameworkCore;
using SyncLib.Abstractions;
using SyncLib.Core.Configuration;
using SyncLib.Core.DependencyInjection;
using SyncLib.EntityFrameworkCore;
using SyncLib.Sample.WebApi;
using SyncLib.Sample.WebApi.Sync;

var builder = WebApplication.CreateBuilder(args);

// Database — use SQL Server when "ConnectionStrings:Default" is set, otherwise
// fall back to InMemory so the sample runs with no infrastructure.
var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseInMemoryDatabase("synclib-sample");
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

// SyncLib core + EF Core state store
builder.Services.AddSyncLibrary();
builder.Services.AddEntityFrameworkSyncStateStore<AppDbContext>();

// Register one provider: weather
builder.Services.AddSyncProvider<WeatherDto, WeatherEntity>("weather")
    .WithConfiguration(new ProviderSyncConfiguration
    {
        ProviderName = "weather",
        SyncInterval = TimeSpan.FromSeconds(30),
        MaxRetryAttempts = 2,
        RetryDelayBase = TimeSpan.FromSeconds(1)
    })
    .WithDataProvider<WeatherApiProvider>()
    .WithRepository<EfSyncRepository<AppDbContext, WeatherEntity>>()
    .WithMapper<WeatherMapper>()
    .Build();

var app = builder.Build();

// Ensure schema (sample only — production should use migrations).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }
}

// ---- Observability endpoints ---------------------------------------------

// All providers' last sync state.
app.MapGet("/sync/state", async (ISyncStateReader reader, CancellationToken ct) =>
    Results.Ok(await reader.GetAllAsync(ct)));

// One provider's state.
app.MapGet("/sync/state/{providerName}", async (string providerName, ISyncStateReader reader, CancellationToken ct) =>
    await reader.GetAsync(providerName, ct) is { } state ? Results.Ok(state) : Results.NotFound());

// Trigger a manual sync now.
app.MapPost("/sync/{providerName}/run", async (string providerName, SyncLib.Core.ISyncOrchestrator orch, CancellationToken ct) =>
{
    await orch.TriggerManualSyncAsync(providerName, ct);
    return Results.Accepted();
});

// Cheap health probe based on sync state.
app.MapGet("/healthz/sync", async (ISyncStateReader reader, CancellationToken ct) =>
{
    var states = await reader.GetAllAsync(ct);
    var unhealthy = states.Where(s => s.LastStatus is SyncStatus.Failed or SyncStatus.Skipped).ToArray();
    return unhealthy.Length == 0
        ? Results.Ok(new { status = "healthy", providers = states.Count })
        : Results.Json(new { status = "degraded", failing = unhealthy.Select(s => s.ProviderName) }, statusCode: 503);
});

// Dump persisted weather rows to demonstrate the sync ran end-to-end.
app.MapGet("/weather", async (AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Weather.AsNoTracking().OrderBy(w => w.City).ToListAsync(ct)));

app.MapGet("/", () => Results.Text("""
    SyncLib sample.
      GET  /sync/state                — last sync state for all providers
      GET  /sync/state/{provider}     — last sync state for one provider
      POST /sync/{provider}/run       — trigger a sync now
      GET  /weather                   — synced rows
      GET  /healthz/sync              — degraded if any provider failed/skipped last
    """));

app.Run();
