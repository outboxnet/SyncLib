using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SyncLib.Abstractions;
using SyncLib.Core.Diagnostics;

namespace SyncLib.Core;

/// <summary>
/// Default <see cref="ISyncRunner"/>. Holds per-provider circuit breakers and
/// applies retry/back-off, the state store, metrics and tracing. Singleton-safe.
/// </summary>
public sealed class SyncRunner : ISyncRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncRunner> _logger;
    private readonly IReadOnlyDictionary<string, SyncJobRegistration> _jobs;
    private readonly IReadOnlyDictionary<string, ISyncConfiguration> _configurations;
    private readonly ConcurrentDictionary<string, CircuitBreaker> _breakers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Constructor used by DI.</summary>
    public SyncRunner(
        IServiceProvider serviceProvider,
        IEnumerable<SyncJobRegistration> registrations,
        IEnumerable<ISyncConfiguration> configurations,
        ILogger<SyncRunner> logger)
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
    public Task RunAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(providerName, out var job))
        {
            throw new ArgumentException($"No sync provider registered with name '{providerName}'.", nameof(providerName));
        }
        return ExecuteOnceAsync(job, _configurations[providerName], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SyncRunSummary> RunAllAsync(CancellationToken cancellationToken = default)
    {
        if (_jobs.Count == 0)
        {
            return new SyncRunSummary(0, 0, 0, 0, new Dictionary<string, string>());
        }

        var errors = new Dictionary<string, string>();
        int succeeded = 0, failed = 0, skipped = 0;

        // Sequential by design: shared resources (DB, breakers) and predictable
        // log ordering matter more than wall-clock for typical sync workloads.
        // Callers that want parallelism can call RunAsync per provider themselves.
        foreach (var job in _jobs.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stateStore = _serviceProvider.GetRequiredService<ISyncStateStore>();
            try
            {
                await ExecuteOnceAsync(job, _configurations[job.ProviderName], cancellationToken).ConfigureAwait(false);
                var state = await stateStore.GetAsync(job.ProviderName, cancellationToken).ConfigureAwait(false);
                switch (state?.LastStatus)
                {
                    case SyncStatus.Succeeded: succeeded++; break;
                    case SyncStatus.Skipped: skipped++; break;
                    case SyncStatus.Failed: failed++; if (state.LastError is { } e) errors[job.ProviderName] = e; break;
                    default: succeeded++; break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // ExecuteOnceAsync already records failure state, but if the
                // store itself throws we still want to surface the provider in
                // the summary instead of aborting the loop.
                failed++;
                errors[job.ProviderName] = ex.Message;
                _logger.LogError(ex, "Unexpected error running provider {ProviderName}", job.ProviderName);
            }
        }

        return new SyncRunSummary(_jobs.Count, succeeded, failed, skipped, errors);
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
