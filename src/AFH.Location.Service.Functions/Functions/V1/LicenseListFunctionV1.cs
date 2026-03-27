using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Api.Mappings.V1;
using AFH.Location.Service.Api.OpenApi;
using AFH.Location.Service.Contract.V1.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Location.Service.Api.Functions.V1;

public sealed class LicenseListFunctionV1
{
    private readonly ILicenseCatalogService _licenseCatalogService;

    public LicenseListFunctionV1(ILicenseCatalogService licenseCatalogService)
    {
        _licenseCatalogService = licenseCatalogService;
    }

    [Function("LicenseListV1")]
    [LocationOpenApiOperation(
        "Advisers",
        "List supported adviser licenses",
        Description = "Function-auth endpoint. Returns the current license catalogue used for location adviser search filtering.",
        SuccessResponseType = typeof(LicenseListResponseV1),
        SuccessDescription = "License catalogue")]
    [LocationOpenApiResponse(401, "Unauthorized", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    [LocationOpenApiResponse(403, "Forbidden", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
    [LocationOpenApiResponse(500, "Server error", ResponseType = typeof(ApiEnvelope<ApiErrorResponseV1>))]
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
