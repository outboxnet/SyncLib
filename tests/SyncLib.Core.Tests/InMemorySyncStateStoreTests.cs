using SyncLib.Abstractions;
using SyncLib.Core;
using Xunit;

namespace SyncLib.Core.Tests;

public class InMemorySyncStateStoreTests
{
    [Fact]
    public async Task GetAsync_ReturnsNull_WhenProviderUnknown()
    {
        var store = new InMemorySyncStateStore();
        Assert.Null(await store.GetAsync("missing"));
    }

    [Fact]
    public async Task RecordSuccess_PopulatesRecordAndIncrementsCounter()
    {
        var store = new InMemorySyncStateStore();
        var startedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await store.RecordRunStartedAsync("p", startedAt);
        await store.RecordSuccessAsync("p", startedAt, TimeSpan.FromSeconds(2), recordCount: 7);

        var state = await store.GetAsync("p");
        Assert.NotNull(state);
        Assert.Equal(SyncStatus.Succeeded, state!.LastStatus);
        Assert.Equal(startedAt, state.LastSuccessAt);
        Assert.Equal(TimeSpan.FromSeconds(2), state.LastDuration);
        Assert.Equal(7, state.LastRecordCount);
        Assert.Equal(1, state.TotalSuccesses);
        Assert.Equal(0, state.ConsecutiveFailures);
    }

    [Fact]
    public async Task RecordFailure_IncrementsConsecutiveFailures_AndStoresMessage()
    {
        var store = new InMemorySyncStateStore();
        var t = DateTime.UtcNow;
        await store.RecordFailureAsync("p", t, TimeSpan.FromMilliseconds(50), new InvalidOperationException("boom"));
        await store.RecordFailureAsync("p", t, TimeSpan.FromMilliseconds(50), new InvalidOperationException("boom2"));

        var state = await store.GetAsync("p");
        Assert.Equal(2, state!.TotalFailures);
        Assert.Equal(2, state.ConsecutiveFailures);
        Assert.Equal("boom2", state.LastError);
    }

    [Fact]
    public async Task SuccessAfterFailure_ResetsConsecutiveCounter()
    {
        var store = new InMemorySyncStateStore();
        var t = DateTime.UtcNow;
        await store.RecordFailureAsync("p", t, TimeSpan.Zero, new Exception("x"));
        await store.RecordSuccessAsync("p", t, TimeSpan.FromMilliseconds(1), 0);

        var state = await store.GetAsync("p");
        Assert.Equal(0, state!.ConsecutiveFailures);
        Assert.Null(state.LastError);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProviders()
    {
        var store = new InMemorySyncStateStore();
        await store.RecordRunStartedAsync("a", DateTime.UtcNow);
        await store.RecordRunStartedAsync("b", DateTime.UtcNow);

        var all = await store.GetAllAsync();
        Assert.Equal(2, all.Count);
    }
}
