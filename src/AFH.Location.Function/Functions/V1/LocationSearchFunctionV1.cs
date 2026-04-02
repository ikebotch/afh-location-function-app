using AFH.Location.Function.Contracts;
using AFH.Location.Application.Abstractions;
using AFH.Location.Function.Mappings.V1;
using AFH.Location.Contract.V1.Requests;
using AFH.Location.Application.Validation.V1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Location.Function.V1;

public sealed class LocationSearchFunctionV1
{
    private readonly ILocationSearchService _service;

    public LocationSearchFunctionV1(ILocationSearchService service)
    {
        _service = service;
    }

    [Function("LocationSearchV1")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/location/inperson/advisers/search")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadFromJsonAsync<LocationSearchRequestV1>(cancellationToken: ct);
        if (payload is null)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "VALIDATION_ERROR", message = "Invalid JSON payload." },
                ct);
        }

        var request = LocationContractMapper.ToApplicationRequest(payload);
        var errors = LocationSearchRequestValidatorV1.Validate(request);
        if (errors.Count > 0)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "VALIDATION_ERROR", message = "Invalid request.", errors },
                ct);
        }

        var result = await _service.SearchInPersonAsync(request, ct);
        var response = LocationContractMapper.ToContractResponse(result);
        var paging = ApiEnvelopeExtensions.SinglePage(response.Candidates.Count);
        return await req.WriteSuccessAsync(response, ct, paging);
    }
}
