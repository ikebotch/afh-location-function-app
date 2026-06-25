using AFH.Adviser.Application.Abstractions.Coverage;
using AFH.Adviser.Application.Models.Auth;
using AFH.Adviser.Application.Models.Coverage;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Function.Functions.V1.Adviser;

public sealed class CoverageAreasFunctionV1
{
    private readonly ICoverageRegionAdminService _regions;
    private readonly IDomainUserAuthorizationService _auth;

    public CoverageAreasFunctionV1(ICoverageRegionAdminService regions, IDomainUserAuthorizationService auth)
    {
        _regions = regions;
        _auth = auth;
    }

    [Function("CoverageAreasListV1")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/coverage-areas")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var regions = await _regions.SearchAsync(new CoverageRegionSearch(null, null, null, IncludeInactive: true), ct);
        var areas = regions
            .Select(region => new CoverageAreaResponseV1(
                region.Id.ToString("D"),
                region.Name,
                region.Postcodes,
                region.AdviserAssignments.Count(assignment => assignment.IsActive),
                region.Id.ToString("D"),
                region.Name,
                region.IsActive ? "Active" : "Inactive",
                region.CreatedUtc))
            .OrderBy(area => area.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var response = new CoverageAreasResponseV1(areas);
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Areas.Count));
    }
}

public sealed record CoverageAreasResponseV1(IReadOnlyList<CoverageAreaResponseV1> Areas);

public sealed record CoverageAreaResponseV1(
    string Id,
    string Name,
    IReadOnlyList<string> Postcodes,
    int AdviserCount,
    string RegionId,
    string RegionName,
    string Status,
    DateTime CreatedAtUtc);
