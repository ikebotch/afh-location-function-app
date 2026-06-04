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
        var candidates = await _assignments.SearchAsync(new OrganisationAssignmentSearch(
            query.Context.Trim(),
            assignmentTypes,
            OrganisationId: null,
            ClientId: null,
            Region: null,
            AdviserId: null,
            IncludeDisabled: false), ct);

        var scoped = ResolveMatches(adviser, candidates, assignmentTypes);

        if (string.IsNullOrWhiteSpace(ResolveOrganisationId(adviser)) ||
            string.IsNullOrWhiteSpace(adviser.Region))
        {
            _logger.LogWarning(
                "Adviser organisation assignment scope is incomplete for AdviserId={AdviserId}. HasOrganisationId={HasOrganisationId}, HasRegion={HasRegion}.",
                adviser.AdviserId,
                !string.IsNullOrWhiteSpace(ResolveOrganisationId(adviser)),
                !string.IsNullOrWhiteSpace(adviser.Region));
        }

        return AdviserScopedOrganisationAssignmentResolution.Ok(scoped);
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

    private static IReadOnlyList<AdviserScopedOrganisationAssignment> ResolveMatches(
        Entities.Adviser adviser,
        IReadOnlyList<OrganisationAssignment> candidates,
        IReadOnlyList<string> requestedTypes)
    {
        var requestedTypeSet = requestedTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matches = candidates
            .Where(x => requestedTypeSet.Contains(x.AssignmentType))
            .Select(x => TryMatch(adviser, x))
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToArray();

        var specificMatches = matches
            .Where(x => !string.Equals(x.Assignment.AssignmentType, FallbackAssignmentType, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (specificMatches.Length > 0)
        {
            return specificMatches
                .GroupBy(x => x.Assignment.AssignmentType, StringComparer.OrdinalIgnoreCase)
                .SelectMany(group =>
                {
                    var bestRank = group.Min(x => x.Rank);
                    return group.Where(x => x.Rank == bestRank);
                })
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Assignment.Priority)
                .ThenBy(x => x.Assignment.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(x => ToScoped(x.Assignment, x))
                .ToArray();
        }

        if (!requestedTypeSet.Contains(FallbackAssignmentType))
            return [];

        return matches
            .Where(x => string.Equals(x.Assignment.AssignmentType, FallbackAssignmentType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Assignment.Priority)
            .ThenBy(x => x.Assignment.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => ToScoped(x.Assignment, x))
            .ToArray();
    }

    private static MatchCandidate? TryMatch(Entities.Adviser adviser, OrganisationAssignment assignment)
    {
        var adviserId = TrimToNull(adviser.AdviserId);
        var organisationId = ResolveOrganisationId(adviser);
        var region = TrimToNull(adviser.Region);

        if (!string.IsNullOrWhiteSpace(assignment.AdviserId) &&
            string.Equals(assignment.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase))
        {
            return new MatchCandidate(
                assignment,
                Rank: 1,
                MatchLevel: OrganisationAssignmentMatchLevels.Adviser,
                MatchedOrganisationId: null,
                MatchedRegion: null,
                MatchedAdviserId: adviserId);
        }

        if (!string.IsNullOrWhiteSpace(organisationId) &&
            !string.IsNullOrWhiteSpace(region) &&
            string.Equals(assignment.OrganisationId, organisationId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(assignment.Region, region, StringComparison.OrdinalIgnoreCase))
        {
            return new MatchCandidate(
                assignment,
                Rank: 2,
                MatchLevel: OrganisationAssignmentMatchLevels.OrganisationRegion,
                MatchedOrganisationId: organisationId,
                MatchedRegion: region,
                MatchedAdviserId: null);
        }

        if (!string.IsNullOrWhiteSpace(organisationId) &&
            string.Equals(assignment.OrganisationId, organisationId, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(assignment.Region))
        {
            return new MatchCandidate(
                assignment,
                Rank: 3,
                MatchLevel: OrganisationAssignmentMatchLevels.Organisation,
                MatchedOrganisationId: organisationId,
                MatchedRegion: null,
                MatchedAdviserId: null);
        }

        if (!string.IsNullOrWhiteSpace(region) &&
            string.IsNullOrWhiteSpace(assignment.OrganisationId) &&
            string.Equals(assignment.Region, region, StringComparison.OrdinalIgnoreCase))
        {
            return new MatchCandidate(
                assignment,
                Rank: 4,
                MatchLevel: OrganisationAssignmentMatchLevels.Region,
                MatchedOrganisationId: null,
                MatchedRegion: region,
                MatchedAdviserId: null);
        }

        if (string.Equals(assignment.AssignmentType, FallbackAssignmentType, StringComparison.OrdinalIgnoreCase))
        {
            return new MatchCandidate(
                assignment,
                Rank: 5,
                MatchLevel: OrganisationAssignmentMatchLevels.Fallback,
                MatchedOrganisationId: null,
                MatchedRegion: null,
                MatchedAdviserId: null);
        }

        return null;
    }

    private static AdviserScopedOrganisationAssignment ToScoped(
        OrganisationAssignment assignment,
        MatchCandidate match)
        => new(
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

    private readonly record struct MatchCandidate(
        OrganisationAssignment Assignment,
        int Rank,
        string MatchLevel,
        string? MatchedOrganisationId,
        string? MatchedRegion,
        string? MatchedAdviserId);
}
