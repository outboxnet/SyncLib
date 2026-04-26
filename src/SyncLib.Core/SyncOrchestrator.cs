// Core/SyncOrchestrator.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyncLibrary.Abstractions;
using System.Collections.Concurrent;

namespace SyncLibrary.Core;

public class SyncOrchestrator : BackgroundService, ISyncOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, SyncJob> _syncJobs;
    private readonly ConcurrentDictionary<string, CircuitBreaker> _circuitBreakers;

    public SyncOrchestrator(IServiceProvider serviceProvider, ILogger<SyncOrchestrator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _syncJobs = new ConcurrentDictionary<string, SyncJob>();
        _circuitBreakers = new ConcurrentDictionary<string, CircuitBreaker>();
    }

    public void RegisterSyncJob<TData, TEntity>(
        ISyncConfiguration configuration,
        ISyncErrorHandler? errorHandler = null)
        where TData : class
        where TEntity : class, IEntity, new()
    {
        var job = new SyncJob
        {
            Configuration = configuration,
            ErrorHandler = errorHandler,
            DataType = typeof(TData),
            EntityType = typeof(TEntity),
            ExecuteAsync = async (cancellationToken) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var dataProvider = scope.ServiceProvider.GetRequiredService<ISyncDataProvider<TData>>();
                var repository = scope.ServiceProvider.GetRequiredService<ISyncRepository<TEntity>>();
                var mapper = scope.ServiceProvider.GetRequiredService<ISyncMapper<TData, TEntity>>();

                var circuitBreaker = _circuitBreakers.GetOrAdd(configuration.ProviderName,
                    _ => new CircuitBreaker(configuration.FailureThreshold, configuration.CircuitBreakerTimeout, _logger));

                await ExecuteSyncWithCircuitBreakerAsync(
                    dataProvider, repository, mapper, configuration,
                    circuitBreaker, errorHandler, cancellationToken);
            }
        };

        _syncJobs[configuration.ProviderName] = job;
        _logger.LogInformation("Registered sync job for provider: {ProviderName}", configuration.ProviderName);
    }

    private async Task ExecuteSyncWithCircuitBreakerAsync<TData, TEntity>(
        ISyncDataProvider<TData> dataProvider,
        ISyncRepository<TEntity> repository,
        ISyncMapper<TData, TEntity> mapper,
        ISyncConfiguration configuration,
        CircuitBreaker circuitBreaker,
        ISyncErrorHandler? errorHandler,
        CancellationToken cancellationToken)
        where TData : class
        where TEntity : class, IEntity, new()
    {
        if (circuitBreaker.IsOpen)
        {
            _logger.LogWarning("Circuit breaker is open for {ProviderName}. Skipping sync.", configuration.ProviderName);
            await errorHandler?.OnSyncErrorAsync(configuration.ProviderName,
                new CircuitBreakerOpenException("Circuit breaker is open"), 0, cancellationToken)!;
            return;
        }

        for (int retry = 0; retry <= configuration.MaxRetryAttempts; retry++)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var lastSyncTime = await repository.GetLastSyncTimeAsync(cancellationToken);

                IEnumerable<TData> data;
                if (lastSyncTime.HasValue)
                {
                    data = await dataProvider.FetchDataAsync(lastSyncTime, cancellationToken);
                }
                else
                {
                    data = await dataProvider.FetchDataAsync(cancellationToken);
                }

                var entities = mapper.MapToEntities(data);
                await repository.AddOrUpdateBatchAsync(entities, cancellationToken);

                var duration = DateTime.UtcNow - startTime;
                circuitBreaker.RecordSuccess();
                await (errorHandler?.OnSyncSuccessAsync(configuration.ProviderName,
                    entities.Count(), duration, cancellationToken) ?? Task.CompletedTask);

                _logger.LogInformation("Sync completed for {ProviderName}: {Count} records in {Duration}",
                    configuration.ProviderName, entities.Count(), duration);
                return;
            }
            catch (Exception ex) when (retry < configuration.MaxRetryAttempts)
            {
                var delay = configuration.RetryDelayBase * Math.Pow(2, retry);
                _logger.LogWarning(ex, "Sync failed for {ProviderName} (Attempt {Retry}/{MaxRetries}). Retrying in {Delay}ms",
                    configuration.ProviderName, retry + 1, configuration.MaxRetryAttempts, delay);

                await Task.Delay(delay, cancellationToken);
                await (errorHandler?.OnSyncErrorAsync(configuration.ProviderName, ex, retry + 1, cancellationToken) ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed permanently for {ProviderName}", configuration.ProviderName);
                circuitBreaker.RecordFailure();
                await (errorHandler?.OnSyncErrorAsync(configuration.ProviderName, ex, configuration.MaxRetryAttempts + 1, cancellationToken) ?? Task.CompletedTask);
                throw;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timers = new List<Timer>();

        foreach (var job in _syncJobs.Values)
        {
            var timer = new Timer(async _ =>
            {
                try
                {
                    await job.ExecuteAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in sync job for {ProviderName}", job.Configuration.ProviderName);
                }
            }, null, TimeSpan.Zero, job.Configuration.SyncInterval);

            timers.Add(timer);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            foreach (var timer in timers)
            {
                await timer.DisposeAsync();
            }
        }
    }

    public async Task TriggerManualSyncAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (_syncJobs.TryGetValue(providerName, out var job))
        {
            _logger.LogInformation("Manual sync triggered for {ProviderName}", providerName);
            await job.ExecuteAsync(cancellationToken);
        }
        else
        {
            throw new ArgumentException($"No sync job registered for provider: {providerName}");
        }
    }

    private class SyncJob
    {
        public ISyncConfiguration Configuration { get; set; } = null!;
        public ISyncErrorHandler? ErrorHandler { get; set; }
        public Type DataType { get; set; } = null!;
        public Type EntityType { get; set; } = null!;
        public Func<CancellationToken, Task> ExecuteAsync { get; set; } = null!;
    }
}
