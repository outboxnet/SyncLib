using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyncLib.Abstractions;

namespace SyncLib.Core;

/// <summary>
/// Hosted service that drives <see cref="ISyncRunner"/> on each provider's
/// configured <see cref="ISyncConfiguration.SyncInterval"/>. Use when SyncLib
/// runs inside a long-lived host (Worker Service, ASP.NET, …).
/// </summary>
/// <remarks>
/// For Azure Functions or any externally-scheduled host, use
/// <see cref="ISyncRunner"/> directly via <c>AddSyncRunner()</c> instead.
/// </remarks>
public sealed class SyncOrchestrator : BackgroundService, ISyncOrchestrator
{
    private readonly ISyncRunner _runner;
    private readonly IReadOnlyDictionary<string, ISyncConfiguration> _configurations;
    private readonly ILogger<SyncOrchestrator> _logger;

    /// <summary>DI constructor.</summary>
    public SyncOrchestrator(
        ISyncRunner runner,
        IEnumerable<ISyncConfiguration> configurations,
        ILogger<SyncOrchestrator> logger)
    {
        _runner = runner;
        _configurations = configurations.ToDictionary(c => c.ProviderName, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> RegisteredProviders => _runner.RegisteredProviders;

    /// <inheritdoc />
    public Task TriggerManualSyncAsync(string providerName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual sync triggered for {ProviderName}", providerName);
        return _runner.RunAsync(providerName, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var providers = _runner.RegisteredProviders;
        if (providers.Count == 0)
        {
            _logger.LogInformation("SyncOrchestrator started with no registered providers.");
            return Task.CompletedTask;
        }

        var loops = providers.Select(name => RunProviderLoopAsync(name, _configurations[name], stoppingToken));
        return Task.WhenAll(loops);
    }

    private async Task RunProviderLoopAsync(string providerName, ISyncConfiguration config, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting sync loop for {ProviderName} every {Interval}", providerName, config.SyncInterval);

        // Run immediately, then on interval.
        try
        {
            await _runner.RunAsync(providerName, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Initial run for {ProviderName} ended with handled error.", providerName);
        }

        using var timer = new PeriodicTimer(config.SyncInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await _runner.RunAsync(providerName, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Run loop for {ProviderName} continued past handled error.", providerName);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }
}
