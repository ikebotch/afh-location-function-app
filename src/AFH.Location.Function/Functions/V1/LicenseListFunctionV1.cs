using AFH.Location.Application.Abstractions.Search;
using AFH.Location.Function.Mapping.V1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using AFH.Location.Function.Functions.V1.Docs;
using AFH.Location.Contract.V1.Responses;

namespace AFH.Location.Function.Functions.V1;

public sealed class LicenseListFunctionV1
{
    private readonly ILicenseCatalogService _licenseCatalogService;

    public LicenseListFunctionV1(ILicenseCatalogService licenseCatalogService)
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
        var result = await _licenseCatalogService.GetLicensesAsync(ct);
        var response = LocationContractMapper.ToContractResponse(result);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteAsJsonAsync(response, ct);

        return ok;
    }
}
