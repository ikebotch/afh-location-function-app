using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Api.Mappings.V1;
using AFH.Location.Service.Api.OpenApi;
using AFH.Location.Service.Contract.V1.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Service.Api.Functions.V1;

public sealed class AdviserCoverageFunctionV1
{
    private readonly IAdviserCoverageService _coverageService;

    public AdviserCoverageFunctionV1(IAdviserCoverageService coverageService)
    {
        _coverageService = coverageService;
    }

    [Function("AdviserCoverageV1")]
    [LocationOpenApiOperation(
        "Coverage",
        "Get adviser coverage points",
        Description = "Internal function-auth endpoint used for adviser and region coverage map data.",
        SuccessResponseType = typeof(ApiEnvelope<AdviserCoverageResponseV1>),
        SuccessDescription = "Coverage dataset")]
    [LocationOpenApiResponse(401, "Unauthorized", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    [LocationOpenApiResponse(403, "Forbidden", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    [LocationOpenApiResponse(500, "Server error", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/adviser-coverage")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var result = await _coverageService.GetCoverageAsync(ct);
        var response = LocationContractMapper.ToContractResponse(result);
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Advisers.Count));
    }
}
