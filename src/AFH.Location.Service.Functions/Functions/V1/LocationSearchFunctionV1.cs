using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Api.Mappings.V1;
using AFH.Location.Service.Application.Validation.V1;
using AFH.Location.Service.Api.OpenApi;
using AFH.Location.Service.Contract.V1.Requests;
using AFH.Location.Service.Contract.V1.Responses;
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
    [LocationOpenApiOperation(
        "Search",
        "Search in-person advisers",
        Description = "Function-auth endpoint. Runs the primary in-person adviser search and ranking flow.",
        RequestBodyType = typeof(LocationSearchRequestV1),
        SuccessResponseType = typeof(ApiEnvelope<LocationSearchResponseV1>),
        SuccessDescription = "Ranked adviser candidates")]
    [LocationOpenApiResponse(400, "Validation error", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    [LocationOpenApiResponse(401, "Unauthorized", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    [LocationOpenApiResponse(403, "Forbidden", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    [LocationOpenApiResponse(422, "Destination could not be resolved", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    [LocationOpenApiResponse(500, "Server error", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
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
