using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SyncLib.Abstractions;
using SyncLib.Core;

namespace SyncLib.Sample.AzureFunctions;

/// <summary>
/// HTTP endpoints for inspecting the most recent sync state and triggering an
/// ad-hoc run. Useful for ops dashboards or manual debugging.
/// </summary>
public sealed class SyncStateFunction
{
    private readonly ISyncStateReader _reader;
    private readonly ISyncRunner _runner;

    public SyncStateFunction(ISyncStateReader reader, ISyncRunner runner)
    {
        _reader = reader;
        _runner = runner;
    }

    [Function("GetAllSyncState")]
    public async Task<IActionResult> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sync/state")] HttpRequest req,
        CancellationToken cancellationToken)
        => new OkObjectResult(await _reader.GetAllAsync(cancellationToken));

    [Function("GetSyncState")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sync/state/{providerName}")] HttpRequest req,
        string providerName,
        CancellationToken cancellationToken)
    {
        var state = await _reader.GetAsync(providerName, cancellationToken);
        return state is null ? new NotFoundResult() : new OkObjectResult(state);
    }

    [Function("RunSyncNow")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sync/{providerName}/run")] HttpRequest req,
        string providerName,
        CancellationToken cancellationToken)
    {
        try
        {
            await _runner.RunAsync(providerName, cancellationToken);
            return new AcceptedResult();
        }
        catch (ArgumentException ex)
        {
            return new NotFoundObjectResult(new { error = ex.Message });
        }
    }
}
