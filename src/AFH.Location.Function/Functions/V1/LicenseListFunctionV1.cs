using AFH.Location.Application.Abstractions;
using AFH.Location.Function.Mappings.V1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Location.Function.V1;

public sealed class LicenseListFunctionV1
{
    private readonly ILicenseCatalogService _licenseCatalogService;

    public LicenseListFunctionV1(ILicenseCatalogService licenseCatalogService)
    {
        _licenseCatalogService = licenseCatalogService;
    }

    [Function("LicenseListV1")]
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
