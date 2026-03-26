using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Api.Mappings.V1;
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
