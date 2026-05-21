using AFH.Location.Function.Mapping.V1.Location;
using AFH.Location.Application.Validation.Travel;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Application.Models.Travel;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Contract.V1.Requests.Travel;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Mapping.V1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

using AFH.Location.Function.Docs.V1;
using AFH.Location.Contract.V1.Responses.Travel;

namespace AFH.Location.Function.Functions.V1.Location;

public sealed class RouteTimeFunctionV1
{
    private readonly IRouteTimeService _service;

    public RouteTimeFunctionV1(IRouteTimeService service)
    {
        _service = service;
    }

    [Function("RouteTimeV1")]
    [LocationOpenApiOperation("RouteTime", "Calculate route time",
        Description = "Calculates travel time and distance between a source and a destination coordinate.",
        RequestBodyType = typeof(RouteTimeRequestV1),
        ResponseType = typeof(RouteTimeResponseV1))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/location/route-time")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadFromJsonAsync<RouteTimeRequestV1>(cancellationToken: ct);
        if (payload is null)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "VALIDATION_ERROR", message = "Invalid JSON payload." },
                ct);
        }

        var request = RouteTimeContractMapper.ToApplicationRequest(payload);
        var errors = RouteTimeRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "VALIDATION_ERROR", message = "Invalid request.", errors },
                ct);
        }

        var result = await _service.CalculateAsync(request, ct);
        return await req.WriteSuccessAsync(RouteTimeContractMapper.ToContractResponse(result), ct);
    }
}
