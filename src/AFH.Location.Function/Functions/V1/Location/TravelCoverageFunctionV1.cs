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

public sealed class TravelCoverageFunctionV1
{
    private readonly ITravelCoverageService _service;

    public TravelCoverageFunctionV1(ITravelCoverageService service)
    {
        _service = service;
    }

    [Function("TravelCoverageV1")]
    [LocationOpenApiOperation("TravelCoverage", "Evaluate travel coverage",
        Description = "Evaluates travel times and distances from a source postcode to a list of destinations.",
        RequestBodyType = typeof(TravelCoverageRequestV1),
        ResponseType = typeof(TravelCoverageResponseV1))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/location/travel-coverage")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadFromJsonAsync<TravelCoverageRequestV1>(cancellationToken: ct);
        if (payload is null)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "VALIDATION_ERROR", message = "Invalid JSON payload." },
                ct);
        }

        var request = TravelCoverageContractMapper.ToApplicationRequest(payload);
        var errors = TravelCoverageRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "VALIDATION_ERROR", message = "Invalid request.", errors },
                ct);
        }

        var result = await _service.EvaluateAsync(request, ct);
        return await req.WriteSuccessAsync(TravelCoverageContractMapper.ToContractResponse(result), ct);
    }
}
