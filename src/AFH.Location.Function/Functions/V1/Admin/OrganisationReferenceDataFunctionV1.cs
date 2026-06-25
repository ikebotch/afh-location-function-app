using AFH.Adviser.Application.Abstractions.OrganisationAssignments;
using AFH.Adviser.Application.Models.Auth;
using AFH.Adviser.Application.Models.OrganisationAssignments;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Function.Functions.V1.Admin;

public sealed class OrganisationReferenceDataFunctionV1
{
    private readonly IOrganisationAssignmentAdminService _assignments;
    private readonly IDomainUserAuthorizationService _auth;

    public OrganisationReferenceDataFunctionV1(
        IOrganisationAssignmentAdminService assignments,
        IDomainUserAuthorizationService auth)
    {
        _assignments = assignments;
        _auth = auth;
    }

    [Function("OrganisationBranchesListV1")]
    public async Task<HttpResponseData> ListBranchesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/organisation/branches")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, OrganisationAssignmentPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var assignments = await _assignments.SearchAsync(AllAssignments(), ct);
        var branches = assignments
            .Where(assignment => !string.IsNullOrWhiteSpace(assignment.OrganisationId))
            .GroupBy(assignment => assignment.OrganisationId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OrganisationBranchReferenceResponseV1(
                group.Key,
                group.Key,
                group.Count(),
                group.Any(assignment => assignment.IsEnabled) ? "Active" : "Inactive"))
            .OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var response = new OrganisationBranchesResponseV1(branches);
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Branches.Count));
    }

    [Function("OrganisationRegionsListV1")]
    public async Task<HttpResponseData> ListRegionsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/organisation/regions")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, OrganisationAssignmentPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var assignments = await _assignments.SearchAsync(AllAssignments(), ct);
        var regions = assignments
            .Where(assignment => !string.IsNullOrWhiteSpace(assignment.Region))
            .GroupBy(assignment => assignment.Region!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OrganisationRegionReferenceResponseV1(
                Slug(group.Key),
                group.Key,
                group.Count(),
                group.Any(assignment => assignment.IsEnabled) ? "Active" : "Inactive"))
            .OrderBy(region => region.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var response = new OrganisationRegionsResponseV1(regions);
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Regions.Count));
    }

    private static OrganisationAssignmentSearch AllAssignments()
        => new(null, [], null, null, null, null, IncludeDisabled: true);

    private static string Slug(string value)
        => string.Join(
            "-",
            value.Trim()
                .ToLowerInvariant()
                .Split([' ', '_', '/', '\\', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

public sealed record OrganisationBranchesResponseV1(IReadOnlyList<OrganisationBranchReferenceResponseV1> Branches);

public sealed record OrganisationBranchReferenceResponseV1(
    string Id,
    string Name,
    int AssignmentCount,
    string Status);

public sealed record OrganisationRegionsResponseV1(IReadOnlyList<OrganisationRegionReferenceResponseV1> Regions);

public sealed record OrganisationRegionReferenceResponseV1(
    string Id,
    string Name,
    int AssignmentCount,
    string Status);
