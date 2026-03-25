using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Api.Contracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Service.Api.Functions.V1.Admin;

public sealed class SyncAdviserCacheFunctionV1
{
    private readonly IAdviserCacheSyncService _syncService;

    public SyncAdviserCacheFunctionV1(IAdviserCacheSyncService syncService)
    {
        _syncService = syncService;
    }

    [Function("SyncAdviserCacheV1")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/admin/advisers/cache/sync")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var synced = await _syncService.SyncAsync(null, ct);
        return await req.WriteSuccessAsync(new { synced }, ct);
    }
}
