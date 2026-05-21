using AFH.Location.Function.Mapping.V1.Adviser;
using AFH.Adviser.Application.Abstractions.Sync;
using AFH.Location.Function.Functions.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using AFH.Location.Function.Docs.V1;
using AFH.Adviser.Contract.V1.Responses;

namespace AFH.Location.Function.Functions.V1.Adviser;

public sealed class SyncAdviserCacheFunctionV1
{
    private readonly IAdviserCacheSyncService _syncService;

    public SyncAdviserCacheFunctionV1(IAdviserCacheSyncService syncService)
    {
        _syncService = syncService;
    }

    [Function("SyncAdviserCacheV1")]
    [LocationOpenApiOperation("Admin", "Sync adviser cache",
        Description = "Synchronises the local adviser cache with downstream identity/directories.",
        ResponseType = typeof(SyncAdviserCacheResponseV1))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/admin/advisers/cache/sync")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var synced = await _syncService.SyncAsync(null, ct);
        return await req.WriteSuccessAsync(new SyncAdviserCacheResponseV1 { Synced = synced }, ct);
    }
}
