using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Contracts.V1.Requests;
using AFH.Location.Service.Core.Validation.V1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Location.Service.Api.Functions.V1;

public sealed class LocationSearchFunctionV1
{
    private readonly ILocationSearchService _service;

    public LocationSearchFunctionV1(ILocationSearchService service)
    {
        _service = service;
    }

    [Function("LocationSearchV1")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/location/inperson/advisers/search")]
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

        var errors = LocationSearchRequestValidatorV1.Validate(payload);
        if (errors.Count > 0)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "VALIDATION_ERROR", message = "Invalid request.", errors },
                ct);
        }

        var result = await _service.SearchInPersonAsync(payload, ct);
        var paging = ApiEnvelopeExtensions.SinglePage(result.Candidates.Count);
        return await req.WriteSuccessAsync(result, ct, paging);
    }
}
