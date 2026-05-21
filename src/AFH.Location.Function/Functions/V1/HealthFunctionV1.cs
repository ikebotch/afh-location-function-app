using AFH.Location.Function.Functions.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using AFH.Location.Function.Functions.V1.Docs;
using AFH.Location.Contract.V1.Responses;

namespace AFH.Location.Function.Functions.V1;

public sealed class HealthFunctionV1
{
    [Function("LocationHealthV1")]
    [LocationOpenApiOperation("System", "Check service health",
        Description = "Simple health check endpoint returning the current status of the service.",
        ResponseType = typeof(HealthResponseV1))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/location/health")] HttpRequestData req,
        CancellationToken ct)
    {
        return await req.WriteSuccessAsync(new HealthResponseV1(), ct);
    }
}