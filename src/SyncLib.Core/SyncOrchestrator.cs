using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyncLib.Abstractions;
using SyncLib.Core.Diagnostics;

namespace SyncLib.Core;

/// <summary>
/// Hosted service that runs each registered provider's sync on its configured interval.
/// </summary>
public sealed class SyncOrchestrator : BackgroundService, ISyncOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncOrchestrator> _logger;
    private readonly IReadOnlyDictionary<string, SyncJobRegistration> _jobs;
    private readonly IReadOnlyDictionary<string, ISyncConfiguration> _configurations;
    private readonly ConcurrentDictionary<string, CircuitBreaker> _breakers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Constructs the orchestrator from DI-registered jobs and configurations.</summary>
    public SyncOrchestrator(
        IServiceProvider serviceProvider,
        IEnumerable<SyncJobRegistration> registrations,
        IEnumerable<ISyncConfiguration> configurations,
        ILogger<SyncOrchestrator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        _configurations = configurations.ToDictionary(c => c.ProviderName, StringComparer.OrdinalIgnoreCase);
        _jobs = registrations.ToDictionary(r => r.ProviderName, StringComparer.OrdinalIgnoreCase);

        foreach (var providerName in _jobs.Keys)
        {
            if (!_configurations.ContainsKey(providerName))
            {
                throw new InvalidOperationException(
                    $"Sync provider '{providerName}' is registered but no ISyncConfiguration with that ProviderName was found.");
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> RegisteredProviders => _jobs.Keys.ToArray();

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_jobs.Count == 0)
        {
            _logger.LogInformation("SyncOrchestrator started with no registered providers.");
            return Task.CompletedTask;
        }

        var loops = _jobs.Values.Select(job => RunProviderLoopAsync(job, _configurations[job.ProviderName], stoppingToken));
        return Task.WhenAll(loops);
    }

    private async Task RunProviderLoopAsync(SyncJobRegistration job, ISyncConfiguration config, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting sync loop for {ProviderName} every {Interval}", job.ProviderName, config.SyncInterval);

        // Run immediately, then on interval.
        try
        {
            await ExecuteOnceAsync(job, config, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        using var timer = new PeriodicTimer(config.SyncInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await ExecuteOnceAsync(job, config, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Already logged inside ExecuteOnceAsync; swallow so the loop survives.
                    _logger.LogDebug(ex, "Run loop for {ProviderName} continued past handled error.", job.ProviderName);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    /// <inheritdoc />
    public Task TriggerManualSyncAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(providerName, out var job))
        {
            throw new ArgumentException($"No sync provider registered with name '{providerName}'.", nameof(providerName));
        }
        var config = _configurations[providerName];
        _logger.LogInformation("Manual sync triggered for {ProviderName}", providerName);
        return ExecuteOnceAsync(job, config, cancellationToken);
    }

    private async Task ExecuteOnceAsync(SyncJobRegistration job, ISyncConfiguration config, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var stateStore = sp.GetRequiredService<ISyncStateStore>();
        var errorHandler = sp.GetService<ISyncErrorHandler>();

        using var activity = SyncDiagnostics.ActivitySource.StartActivity("synclib.sync", ActivityKind.Internal);
        activity?.SetTag("sync.provider", job.ProviderName);

        var breaker = _breakers.GetOrAdd(job.ProviderName,
            _ => new CircuitBreaker(config.FailureThreshold, config.CircuitBreakerTimeout, _logger));

        if (config.EnableCircuitBreaker && breaker.IsOpen)
        {
            _logger.LogWarning("Skipping sync for {ProviderName}: circuit breaker is open.", job.ProviderName);
            await stateStore.RecordSkippedAsync(job.ProviderName, DateTime.UtcNow, "Circuit breaker open", cancellationToken).ConfigureAwait(false);
            SyncDiagnostics.SyncSkipped.Add(1, new KeyValuePair<string, object?>("sync.provider", job.ProviderName));
            activity?.SetTag("sync.status", "skipped");
            if (errorHandler is not null)
            {
                await errorHandler.OnSyncErrorAsync(job.ProviderName,
                    new CircuitBreakerOpenException("Circuit breaker is open"), 0, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        var startedAt = DateTime.UtcNow;
        await stateStore.RecordRunStartedAsync(job.ProviderName, startedAt, cancellationToken).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        Exception? lastError = null;

        for (var attempt = 0; attempt <= config.MaxRetryAttempts; attempt++)
        {
            try
            {
                var recordCount = await job.ExecuteAsync(sp, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                breaker.RecordSuccess();
                await stateStore.RecordSuccessAsync(job.ProviderName, startedAt, stopwatch.Elapsed, recordCount, cancellationToken).ConfigureAwait(false);

                var providerTag = new KeyValuePair<string, object?>("sync.provider", job.ProviderName);
                SyncDiagnostics.SyncDuration.Record(stopwatch.Elapsed.TotalMilliseconds, providerTag);
                SyncDiagnostics.SyncSuccesses.Add(1, providerTag);
                SyncDiagnostics.SyncRecords.Add(recordCount, providerTag);

                activity?.SetTag("sync.records", recordCount);
                activity?.SetTag("sync.status", "succeeded");

                if (errorHandler is not null)
                {
                    await errorHandler.OnSyncSuccessAsync(job.ProviderName, recordCount, stopwatch.Elapsed, cancellationToken).ConfigureAwait(false);
                }

                _logger.LogInformation("Sync completed for {ProviderName}: {Count} records in {Duration}",
                    job.ProviderName, recordCount, stopwatch.Elapsed);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (errorHandler is not null)
                {
                    await errorHandler.OnSyncErrorAsync(job.ProviderName, ex, attempt + 1, cancellationToken).ConfigureAwait(false);
                }

                if (attempt < config.MaxRetryAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(config.RetryDelayBase.TotalMilliseconds * Math.Pow(2, attempt));
                    _logger.LogWarning(ex, "Sync failed for {ProviderName} (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}.",
                        job.ProviderName, attempt + 1, config.MaxRetryAttempts, delay);
                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
            }
        }

        // Exhausted retries.
        stopwatch.Stop();
        breaker.RecordFailure();
        await stateStore.RecordFailureAsync(job.ProviderName, startedAt, stopwatch.Elapsed, lastError!, cancellationToken).ConfigureAwait(false);

        var failProviderTag = new KeyValuePair<string, object?>("sync.provider", job.ProviderName);
        SyncDiagnostics.SyncDuration.Record(stopwatch.Elapsed.TotalMilliseconds, failProviderTag);
        SyncDiagnostics.SyncFailures.Add(1, failProviderTag);

        activity?.SetTag("sync.status", "failed");
        activity?.SetStatus(ActivityStatusCode.Error, lastError?.Message);

        _logger.LogError(lastError, "Sync failed permanently for {ProviderName} after {Attempts} attempts.",
            job.ProviderName, config.MaxRetryAttempts + 1);
    }
}
