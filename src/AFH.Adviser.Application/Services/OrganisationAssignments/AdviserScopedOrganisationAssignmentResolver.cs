using AFH.Adviser.Application.Abstractions.OrganisationAssignments;
using AFH.Adviser.Application.Abstractions.Repositories;
using AFH.Adviser.Application.Models.OrganisationAssignments;
using Microsoft.Extensions.Logging;
using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Application.Services.OrganisationAssignments;

public sealed class AdviserScopedOrganisationAssignmentResolver : IAdviserScopedOrganisationAssignmentResolver
{
    private const string FallbackAssignmentType = "Fallback";

    private readonly IAdviserReferenceCacheRepository _advisers;
    private readonly IOrganisationAssignmentDirectory _assignments;
    private readonly ILogger<AdviserScopedOrganisationAssignmentResolver> _logger;

    public AdviserScopedOrganisationAssignmentResolver(
        IAdviserReferenceCacheRepository advisers,
        IOrganisationAssignmentDirectory assignments,
        ILogger<AdviserScopedOrganisationAssignmentResolver> logger)
    {
        _advisers = advisers;
        _assignments = assignments;
        _logger = logger;
    }

    public async Task<AdviserScopedOrganisationAssignmentResolution> ResolveAsync(
        AdviserScopedOrganisationAssignmentQuery query,
        CancellationToken ct)
    {
        var validation = Validate(query);
        if (validation is not null)
            return AdviserScopedOrganisationAssignmentResolution.ValidationFailed(validation);

        var adviserId = query.AdviserId.Trim();
        var adviser = (await _advisers.GetAllAsync([adviserId], ct))
            .FirstOrDefault(x => string.Equals(x.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase));

        if (adviser is null)
            return AdviserScopedOrganisationAssignmentResolution.AdviserNotFound(adviserId);

        var assignmentTypes = NormaliseAssignmentTypes(query.AssignmentTypes);
        var organisationId = ResolveOrganisationId(adviser);
        var region = TrimToNull(adviser.Region);

        if (string.IsNullOrWhiteSpace(organisationId) ||
            string.IsNullOrWhiteSpace(region))
        {
            _logger.LogWarning(
                "Adviser organisation assignment scope is incomplete for AdviserId={AdviserId}. HasOrganisationId={HasOrganisationId}, HasRegion={HasRegion}.",
                adviser.AdviserId,
                !string.IsNullOrWhiteSpace(organisationId),
                !string.IsNullOrWhiteSpace(region));
        }

        var matches = await _assignments.ResolveScopedAsync(new OrganisationAssignmentScopedSearch(
            query.Context.Trim(),
            assignmentTypes,
            adviserId,
            organisationId,
            region,
            ClientId: null,
            IncludeDisabled: false,
            IncludeFallback: assignmentTypes.Contains(FallbackAssignmentType, StringComparer.OrdinalIgnoreCase)), ct);

        return AdviserScopedOrganisationAssignmentResolution.Ok(matches.Select(ToScoped).ToArray());
    }

    private static string? Validate(AdviserScopedOrganisationAssignmentQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.AdviserId))
            return "adviserId is required.";

        if (string.IsNullOrWhiteSpace(query.Context))
            return "context is required.";

        if (NormaliseAssignmentTypes(query.AssignmentTypes).Count == 0)
            return "assignmentTypes is required.";

        return null;
    }

    private static AdviserScopedOrganisationAssignment ToScoped(
        OrganisationAssignmentScopedMatch match)
    {
        var assignment = match.Assignment;
        return new AdviserScopedOrganisationAssignment(
            assignment.Id,
            assignment.Context,
            assignment.AssignmentType,
            assignment.OrganisationId,
            assignment.ClientId,
            assignment.Region,
            assignment.AdviserId,
            assignment.DisplayName,
            assignment.Email,
            assignment.MobileNumber,
            assignment.Channels,
            assignment.IsEnabled,
            assignment.Priority,
            match.MatchLevel,
            match.MatchedOrganisationId,
            match.MatchedRegion,
            match.MatchedAdviserId);
    }

    private static IReadOnlyList<string> NormaliseAssignmentTypes(IEnumerable<string> assignmentTypes)
        => assignmentTypes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string? ResolveOrganisationId(Entities.Adviser adviser)
        // The current adviser cache model carries the adviser organisation scope in BaseOfficeId.
        // Keep the resolver isolated here so a future explicit OrganisationId column is a local change.
        => TrimToNull(adviser.BaseOfficeId);

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
