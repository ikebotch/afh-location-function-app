using AFH.Adviser.Application.Admin;
using AFH.Adviser.Contract.V1.Responses;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Functions.V1.Docs;
using AFH.Location.Function.Mapping.V1;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Function.Functions.V1.Admin;

public sealed class AdviserCoverageFunctionV1
{
    private readonly IAdviserCoverageFeedService _feedService;

    public AdviserCoverageFunctionV1(IAdviserCoverageFeedService feedService)
    {
        _feedService = feedService;
    }

    [Function("AdviserCoverageV1")]
    [LocationOpenApiOperation("Admin", "Get adviser coverage",
        Description = "Retrieves geographical coverage points for advisers and regions for mapping on the dashboard.",
        ResponseType = typeof(AdviserCoverageResponseV1))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/adviser-coverage")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var query = QueryHelpers.ParseQuery(req.Url.Query);
        DateTime? sinceUtc = null;
        
        if (query.TryGetValue("sinceUtc", out var sinceUtcValues) && 
            DateTime.TryParse(sinceUtcValues.FirstOrDefault(), out var parsedSinceUtc))
        {
            sinceUtc = parsedSinceUtc;
        }

        var result = await _feedService.GetCoverageFeedAsync(ct);
        var response = AdviserCoverageContractMapper.ToContractResponse(result);
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Advisers.Count));
    }
}
