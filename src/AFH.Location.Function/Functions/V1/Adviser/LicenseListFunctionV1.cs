using AFH.Location.Function.Mapping.V1.Adviser;
using AFH.Adviser.Application.Abstractions.Skills;
using AFH.Location.Function.Mapping.V1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using AFH.Location.Function.Docs.V1;
using AFH.Location.Contract.V1.Responses;

namespace AFH.Location.Function.Functions.V1.Adviser;

public sealed class LicenseListFunctionV1
{
    private readonly IAdviserSkillCatalogService _licenseCatalogService;

    public LicenseListFunctionV1(IAdviserSkillCatalogService licenseCatalogService)
    {
        _licenseCatalogService = licenseCatalogService;
    }

    [Function("LicenseListV1")]
    [LocationOpenApiOperation("System", "List third-party licenses",
        Description = "Returns a list of third-party open source licenses used by the service.",
        ResponseType = typeof(LicenseListResponseV1))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/location/licenses")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var result = await _licenseCatalogService.GetSkillsAsync(ct);
        var response = LicenseCatalogContractMapper.ToContractResponse(result);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteAsJsonAsync(response, ct);

        return ok;
    }
}
