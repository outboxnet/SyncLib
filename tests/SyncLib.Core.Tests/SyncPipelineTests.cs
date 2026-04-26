using Microsoft.Extensions.DependencyInjection;
using SyncLib.Abstractions;
using SyncLib.Core;
using Xunit;

namespace SyncLib.Core.Tests;

public class SyncPipelineTests
{
    [Fact]
    public async Task Execute_FullFetch_WhenNoPriorSuccess()
    {
        var (sp, provider, repo) = BuildScope(lastSuccess: null);

        var count = await new SyncPipeline<Dto, Ent>("p").ExecuteAsync(sp, default);

        Assert.Equal(2, count);
        Assert.Null(provider.LastFetchSince);
        Assert.Equal(2, repo.UpsertedBatches.Single().Count);
    }

    [Fact]
    public async Task Execute_IncrementalFetch_UsesLastSuccessAt()
    {
        var since = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var (sp, provider, _) = BuildScope(lastSuccess: since);

        await new SyncPipeline<Dto, Ent>("p").ExecuteAsync(sp, default);

        Assert.Equal(since, provider.LastFetchSince);
    }

    [Fact]
    public async Task DerivedPipeline_CanOverrideFetch()
    {
        var (sp, provider, _) = BuildScope(lastSuccess: DateTime.UtcNow);

        await new ForceFullFetchPipeline("p").ExecuteAsync(sp, default);

        Assert.Null(provider.LastFetchSince); // override ignored the lastSuccessAt
    }

    [Fact]
    public void Ctor_RejectsEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new SyncPipeline<Dto, Ent>(""));
    }

    private static (IServiceProvider sp, FakeProvider provider, FakeRepo repo) BuildScope(DateTime? lastSuccess)
    {
        var services = new ServiceCollection();
        var store = new InMemorySyncStateStore();
        if (lastSuccess is { } t)
        {
            // Seed a prior success so the pipeline reads it.
            store.RecordSuccessAsync("p", t, TimeSpan.FromSeconds(1), recordCount: 5).GetAwaiter().GetResult();
        }
        services.AddSingleton<ISyncStateStore>(store);
        var provider = new FakeProvider();
        var repo = new FakeRepo();
        services.AddSingleton<ISyncDataProvider<Dto>>(provider);
        services.AddSingleton<ISyncRepository<Ent>>(repo);
        services.AddSingleton<ISyncMapper<Dto, Ent>, FakeMapper>();
        return (services.BuildServiceProvider(), provider, repo);
    }

    public sealed record Dto(int N);

    public sealed class Ent : IEntity
    {
        public Guid Id { get; set; }
        public int N { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private sealed class FakeProvider : ISyncDataProvider<Dto>
    {
        public string ProviderName => "p";
        public DateTime? LastFetchSince { get; private set; }
        public Task<IReadOnlyCollection<Dto>> FetchDataAsync(CancellationToken cancellationToken = default)
        {
            LastFetchSince = null;
            return Task.FromResult<IReadOnlyCollection<Dto>>([new Dto(1), new Dto(2)]);
        }
        public Task<IReadOnlyCollection<Dto>> FetchDataAsync(DateTime? lastSyncTime, CancellationToken cancellationToken = default)
        {
            LastFetchSince = lastSyncTime;
            return Task.FromResult<IReadOnlyCollection<Dto>>([new Dto(1), new Dto(2)]);
        }
    }

    private sealed class FakeRepo : ISyncRepository<Ent>
    {
        public List<IReadOnlyCollection<Ent>> UpsertedBatches { get; } = new();
        public Task AddOrUpdateBatchAsync(IEnumerable<Ent> entities, CancellationToken cancellationToken = default)
        {
            UpsertedBatches.Add(entities.ToArray());
            return Task.CompletedTask;
        }
        public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task ClearOldDataAsync(DateTime olderThan, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMapper : ISyncMapper<Dto, Ent>
    {
        public Ent MapToEntity(Dto data) => new() { Id = Guid.NewGuid(), N = data.N };
        public IReadOnlyCollection<Ent> MapToEntities(IEnumerable<Dto> data) => data.Select(MapToEntity).ToArray();
    }

    private sealed class ForceFullFetchPipeline : SyncPipeline<Dto, Ent>
    {
        public ForceFullFetchPipeline(string providerName) : base(providerName) { }
        protected override Task<IReadOnlyCollection<Dto>> FetchAsync(
            ISyncDataProvider<Dto> dataProvider, DateTime? lastSuccessAt, CancellationToken cancellationToken)
            => dataProvider.FetchDataAsync(cancellationToken); // ignore lastSuccessAt
    }
}
