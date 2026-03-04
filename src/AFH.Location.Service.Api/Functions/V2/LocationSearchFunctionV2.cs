using AFH.Location.Service.Core.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Location.Service.Api.Functions.V2;

public sealed class LocationSearchFunctionV2
{
    private readonly IRoutingService _routing;
    public LocationSearchFunctionV2(IRoutingService routing) => _routing = routing;

    [Function("LocationSearchV2")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v2/location/inperson/advisers/search")] HttpRequestData req)
    {
        var _ = await _routing.GetRouteAsync((0, 0), (0, 0), CancellationToken.None);

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteStringAsync("v2 OK (Google Maps wired)");
        return res;
    }
}