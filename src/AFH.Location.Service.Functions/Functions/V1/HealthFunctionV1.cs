using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Api.OpenApi;
using AFH.Location.Service.Contract.V1.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Service.Api.Functions.V1;

public sealed class HealthFunctionV1
{
    [Function("LocationHealthV1")]
    [LocationOpenApiOperation(
        "Health",
        "Health check",
        Description = "Anonymous health endpoint for the Location service.",
        SuccessResponseType = typeof(ApiEnvelope<HealthStatusResponseV1>),
        SuccessDescription = "Service healthy")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/location/health")] HttpRequestData req,
        CancellationToken ct)
    {
        return await req.WriteSuccessAsync(new HealthStatusResponseV1
        {
            Status = "Healthy"
        }, ct);
    }
}
