using AFH.Location.Service.Api.Contracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Service.Api.Functions.V1;

public sealed class HealthFunctionV1
{
    [Function("LocationHealthV1")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/location/health")] HttpRequestData req,
        CancellationToken ct)
    {
        return await req.WriteSuccessAsync(new
        {
            status = "Healthy"
        }, ct);
    }
}
