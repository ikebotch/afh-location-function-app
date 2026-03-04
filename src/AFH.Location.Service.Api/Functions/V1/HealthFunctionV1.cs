using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Location.Service.Api.Functions.V1;

public sealed class HealthFunctionV1
{
    [Function("LocationHealthV1")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/location/health")] HttpRequestData req)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.WriteStringAsync("Healthy");
        return res;
    }
}