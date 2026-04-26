using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SyncLib.Abstractions;
using SyncLib.Core;
using SyncLib.Core.Configuration;
using SyncLib.Core.DependencyInjection;
using Xunit;

namespace SyncLib.Core.Tests;

public class SyncOrchestratorTests
{
    [Fact]
    public async Task TriggerManualSync_RunsProvider_AndRecordsSuccess()
    {
        var services = new ServiceCollection();
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();
        services.AddSyncLibrary();
        services.AddSyncProvider<FakeDto, FakeEntity>("fake")
            .WithConfiguration(new ProviderSyncConfiguration
            {
                ProviderName = "fake",
                SyncInterval = TimeSpan.FromHours(1),
                MaxRetryAttempts = 0
            })
            .WithDataProvider<FakeDataProvider>()
            .WithRepository<FakeRepository>()
            .WithMapper<FakeMapper>()
            .Build();

        await using var sp = services.BuildServiceProvider();
        var orch = sp.GetRequiredService<ISyncOrchestrator>();

        await orch.TriggerManualSyncAsync("fake");

        var state = await sp.GetRequiredService<ISyncStateReader>().GetAsync("fake");
        Assert.NotNull(state);
        Assert.Equal(SyncStatus.Succeeded, state!.LastStatus);
        Assert.Equal(3, state.LastRecordCount);
    }

    [Fact]
    public async Task TriggerManualSync_RetriesUntilSuccess()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSyncLibrary();
        services.AddSyncProvider<FakeDto, FakeEntity>("flaky")
            .WithConfiguration(new ProviderSyncConfiguration
            {
                ProviderName = "flaky",
                SyncInterval = TimeSpan.FromHours(1),
                MaxRetryAttempts = 2,
                RetryDelayBase = TimeSpan.FromMilliseconds(1)
            })
            .WithDataProvider<FlakyProvider>()
            .WithRepository<FakeRepository>()
            .WithMapper<FakeMapper>()
            .Build();

        await using var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<ISyncOrchestrator>().TriggerManualSyncAsync("flaky");

        var state = await sp.GetRequiredService<ISyncStateReader>().GetAsync("flaky");
        Assert.Equal(SyncStatus.Succeeded, state!.LastStatus);
    }

    [Fact]
    public async Task TriggerManualSync_RecordsFailure_AfterRetriesExhausted()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSyncLibrary();
        services.AddSyncProvider<FakeDto, FakeEntity>("broken")
            .WithConfiguration(new ProviderSyncConfiguration
            {
                ProviderName = "broken",
                SyncInterval = TimeSpan.FromHours(1),
                MaxRetryAttempts = 1,
                RetryDelayBase = TimeSpan.FromMilliseconds(1),
                EnableCircuitBreaker = false
            })
            .WithDataProvider<AlwaysFailsProvider>()
            .WithRepository<FakeRepository>()
            .WithMapper<FakeMapper>()
            .Build();

        await using var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<ISyncOrchestrator>().TriggerManualSyncAsync("broken");

        var state = await sp.GetRequiredService<ISyncStateReader>().GetAsync("broken");
        Assert.Equal(SyncStatus.Failed, state!.LastStatus);
        Assert.Equal(1, state.TotalFailures);
        Assert.NotNull(state.LastError);
    }

    [Fact]
    public async Task TriggerManualSync_Throws_ForUnknownProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSyncLibrary();

        await using var sp = services.BuildServiceProvider();
        var orch = sp.GetRequiredService<ISyncOrchestrator>();

        await Assert.ThrowsAsync<ArgumentException>(() => orch.TriggerManualSyncAsync("nope"));
    }

    // ---- Test doubles --------------------------------------------------

    public sealed record FakeDto(int N);

    public sealed class FakeEntity : IEntity
    {
        public Guid Id { get; set; }
        public int N { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private sealed class FakeDataProvider : ISyncDataProvider<FakeDto>
    {
        public string ProviderName => "fake";
        public Task<IReadOnlyCollection<FakeDto>> FetchDataAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<FakeDto>>(new[] { new FakeDto(1), new FakeDto(2), new FakeDto(3) });
        public Task<IReadOnlyCollection<FakeDto>> FetchDataAsync(DateTime? lastSyncTime, CancellationToken cancellationToken = default) =>
            FetchDataAsync(cancellationToken);
    }

    private sealed class FlakyProvider : ISyncDataProvider<FakeDto>
    {
        private int _calls;
        public string ProviderName => "flaky";
        public Task<IReadOnlyCollection<FakeDto>> FetchDataAsync(CancellationToken cancellationToken = default)
        {
            _calls++;
            if (_calls < 2) throw new InvalidOperationException("transient");
            return Task.FromResult<IReadOnlyCollection<FakeDto>>(new[] { new FakeDto(1) });
        }
        public Task<IReadOnlyCollection<FakeDto>> FetchDataAsync(DateTime? lastSyncTime, CancellationToken cancellationToken = default) =>
            FetchDataAsync(cancellationToken);
    }

    private sealed class AlwaysFailsProvider : ISyncDataProvider<FakeDto>
    {
        public string ProviderName => "broken";
        public Task<IReadOnlyCollection<FakeDto>> FetchDataAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("nope");
        public Task<IReadOnlyCollection<FakeDto>> FetchDataAsync(DateTime? lastSyncTime, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("nope");
    }

    private sealed class FakeRepository : ISyncRepository<FakeEntity>
    {
        public Task AddOrUpdateBatchAsync(IEnumerable<FakeEntity> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task ClearOldDataAsync(DateTime olderThan, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMapper : ISyncMapper<FakeDto, FakeEntity>
    {
        public FakeEntity MapToEntity(FakeDto data) => new() { Id = Guid.NewGuid(), N = data.N };
        public IReadOnlyCollection<FakeEntity> MapToEntities(IEnumerable<FakeDto> data) => data.Select(MapToEntity).ToArray();
    }
}
