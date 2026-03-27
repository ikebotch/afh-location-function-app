using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Api.OpenApi;
using AFH.Location.Service.Contract.V1.Responses;
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
    [LocationOpenApiOperation(
        "Admin/Internal",
        "Sync adviser cache",
        Description = "Internal function-auth endpoint. Refreshes the adviser reference cache used by location search.",
        SuccessResponseType = typeof(ApiEnvelope<SyncAdviserCacheResponseV1>),
        SuccessDescription = "Adviser cache sync completed")]
    [LocationOpenApiResponse(401, "Unauthorized", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    [LocationOpenApiResponse(403, "Forbidden", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    [LocationOpenApiResponse(500, "Server error", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/admin/advisers/cache/sync")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var synced = await _syncService.SyncAsync(null, ct);
        return await req.WriteSuccessAsync(new SyncAdviserCacheResponseV1
        {
            Synced = synced
        }, ct);
    }
}
