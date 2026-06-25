using System.Net;
using AFH.Adviser.Application.Abstractions.Coverage;
using AFH.Adviser.Application.Models.Auth;
using AFH.Adviser.Application.Models.Coverage;
using AFH.Adviser.Contract.V1.Coverage;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Function.Functions.V1.Adviser;

public sealed class CoverageRegionsFunctionV1
{
    private readonly ICoverageRegionAdminService _regions;
    private readonly IDomainUserAuthorizationService _auth;

    public CoverageRegionsFunctionV1(ICoverageRegionAdminService regions, IDomainUserAuthorizationService auth)
    {
        _regions = regions;
        _auth = auth;
    }

    [Function("CoverageRegionsListV1")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/coverage-regions")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var regions = await _regions.SearchAsync(ToSearch(req.Url.Query), ct);
        var response = new CoverageRegionsResponseV1(regions.Select(ToDto).ToArray());
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Regions.Count));
    }

    [Function("CoverageRegionsGetV1")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/coverage-regions/{id:guid}")]
        HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var region = await _regions.GetAsync(id, ct);
        return region is null
            ? await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "COVERAGE_REGION_NOT_FOUND", message = "Coverage region was not found." }, ct)
            : await req.WriteSuccessAsync(ToDto(region), ct);
    }

    [Function("CoverageRegionsCreateV1")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/coverage-regions")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Create, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<CoverageRegionUpsertRequestV1>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_COVERAGE_REGION", message = validation }, ct);

        var created = await _regions.CreateAsync(ToUpsert(body!), ct);
        return await req.WriteSuccessAsync(ToDto(created), ct, statusCode: HttpStatusCode.Created);
    }

    [Function("CoverageRegionsUpdateV1")]
    public async Task<HttpResponseData> UpdateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/coverage-regions/{id:guid}")]
        HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Update, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<CoverageRegionUpsertRequestV1>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_COVERAGE_REGION", message = validation }, ct);

        var updated = await _regions.UpdateAsync(id, ToUpsert(body!), ct);
        return updated is null
            ? await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "COVERAGE_REGION_NOT_FOUND", message = "Coverage region was not found." }, ct)
            : await req.WriteSuccessAsync(ToDto(updated), ct);
    }

    [Function("CoverageRegionsDisableV1")]
    public async Task<HttpResponseData> DisableAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/coverage-regions/{id:guid}/disable")]
        HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Disable, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var disabled = await _regions.DisableAsync(id, ct);
        return disabled
            ? await req.WriteSuccessAsync(new { id, disabled = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "COVERAGE_REGION_NOT_FOUND", message = "Coverage region was not found." }, ct);
    }

    [Function("CoverageRegionsDeleteV1")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/coverage-regions/{id:guid}")]
        HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Delete, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var deleted = await _regions.DeleteAsync(id, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { id, deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "COVERAGE_REGION_NOT_FOUND", message = "Coverage region was not found." }, ct);
    }

    [Function("CoverageRegionsAssignAdviserV1")]
    public async Task<HttpResponseData> AssignAdviserAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/coverage-regions/{regionId:guid}/advisers")]
        HttpRequestData req,
        Guid regionId,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.AssignAdvisers, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<AdviserRegionAssignmentUpsertRequestV1>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.AdviserId) || string.IsNullOrWhiteSpace(body.AdviserName))
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_ADVISER_REGION_ASSIGNMENT", message = "adviserId and adviserName are required." }, ct);

        try
        {
            var assignment = await _regions.AssignAdviserAsync(
                new AdviserRegionAssignmentUpsert(
                    regionId,
                    body.AdviserId!,
                    body.AdviserName!,
                    body.Role,
                    body.IsLead ?? false,
                    body.IsActive ?? true),
                ct);

            return await req.WriteSuccessAsync(ToDto(assignment), ct, statusCode: HttpStatusCode.Created);
        }
        catch (InvalidOperationException)
        {
            return await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "COVERAGE_REGION_NOT_FOUND", message = "Coverage region was not found." }, ct);
        }
    }

    [Function("CoverageRegionsRemoveAdviserV1")]
    public async Task<HttpResponseData> RemoveAdviserAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/coverage-regions/adviser-assignments/{assignmentId:guid}")]
        HttpRequestData req,
        Guid assignmentId,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.AssignAdvisers, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var removed = await _regions.RemoveAdviserAsync(assignmentId, ct);
        return removed
            ? await req.WriteSuccessAsync(new { assignmentId, removed = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ADVISER_REGION_ASSIGNMENT_NOT_FOUND", message = "Adviser region assignment was not found." }, ct);
    }

    private static CoverageRegionSearch ToSearch(string queryString)
    {
        var query = QueryHelpers.ParseQuery(queryString);
        return new CoverageRegionSearch(
            Get(query, "search"),
            Get(query, "regionCode"),
            Get(query, "adviserId"),
            GetBool(query, "includeInactive"));
    }

    private static string? Validate(CoverageRegionUpsertRequestV1? request)
    {
        if (request is null)
            return "Request body is required.";
        if (string.IsNullOrWhiteSpace(request.Code))
            return "code is required.";
        if (string.IsNullOrWhiteSpace(request.Name))
            return "name is required.";
        if (request.CoverageRadiusMiles is < 0)
            return "coverageRadiusMiles cannot be negative.";
        if (request.MaxTravelTimeMinutes is < 0)
            return "maxTravelTimeMinutes cannot be negative.";
        return null;
    }

    private static CoverageRegionUpsert ToUpsert(CoverageRegionUpsertRequestV1 request)
        => new(
            request.Code!,
            request.Name!,
            request.LeadAdviserId,
            request.LeadAdviserName,
            request.Postcodes ?? [],
            request.Skills ?? [],
            request.CoverageRadiusMiles ?? 0,
            request.MaxTravelTimeMinutes ?? 0,
            request.IsActive ?? true);

    private static CoverageRegionDto ToDto(CoverageRegion region)
        => new(
            region.Id,
            region.Code,
            region.Name,
            region.LeadAdviserId,
            region.LeadAdviserName,
            region.Postcodes,
            region.Skills,
            region.CoverageRadiusMiles,
            region.MaxTravelTimeMinutes,
            region.IsActive,
            region.CreatedUtc,
            region.UpdatedUtc,
            region.AdviserAssignments.Select(ToDto).ToArray());

    private static AdviserRegionAssignmentDto ToDto(AdviserRegionAssignment assignment)
        => new(
            assignment.Id,
            assignment.RegionId,
            assignment.RegionCode,
            assignment.AdviserId,
            assignment.AdviserName,
            assignment.Role,
            assignment.IsLead,
            assignment.IsActive,
            assignment.CreatedUtc,
            assignment.UpdatedUtc);

    private static string? Get(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
        => query.TryGetValue(key, out var value) ? value.FirstOrDefault() : null;

    private static bool GetBool(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
        => query.TryGetValue(key, out var values)
            && bool.TryParse(values.FirstOrDefault(), out var parsed)
            && parsed;
}
