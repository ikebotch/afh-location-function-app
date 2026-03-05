using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Contracts.V1.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Service.Api.Functions.V1;

public sealed class LicenseListFunctionV1
{
    private readonly IAdviserRepository _adviserRepository;

    public LicenseListFunctionV1(IAdviserRepository adviserRepository)
    {
        _adviserRepository = adviserRepository;
    }

    [Function("LicenseListV1")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/location/licenses")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var advisers = await _adviserRepository.GetAllAsync(null, ct);

        var licenses = advisers
            .Where(a => a.IsActive)
            .SelectMany(a => a.Skills)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var payload = new LicenseListResponseV1
        {
            Licenses = licenses
        };

        var paging = ApiEnvelopeExtensions.SinglePage(licenses.Length);
        return await req.WriteSuccessAsync(payload, ct, paging);
    }
}
