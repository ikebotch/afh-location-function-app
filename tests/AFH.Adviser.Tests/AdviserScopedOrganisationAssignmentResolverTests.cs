using AFH.Adviser.Application.Abstractions.OrganisationAssignments;
using AFH.Adviser.Application.Abstractions.Repositories;
using AFH.Adviser.Application.Models.OrganisationAssignments;
using AFH.Adviser.Application.Services.OrganisationAssignments;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Tests;

public sealed class AdviserScopedOrganisationAssignmentResolverTests
{
    [Fact]
    public async Task ResolveAsync_UsesAdviserOrganisationAndRegionScope()
    {
        var sut = CreateSut(
            Adviser("adv-1", organisationId: "org-1", region: "Region A"),
            [
                Assignment("Region A Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A"),
                Assignment("Region B Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region B")
            ]);

        var result = await sut.ResolveAsync(Query("adv-1", ["ContactCentre"]), CancellationToken.None);

        Assert.Equal(AdviserScopedOrganisationAssignmentResolutionStatus.Succeeded, result.Status);
        var assignment = Assert.Single(result.Assignments);
        Assert.Equal("Region A Contact Centre", assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.OrganisationRegion, assignment.MatchLevel);
        Assert.Equal("org-1", assignment.MatchedOrganisationId);
        Assert.Equal("Region A", assignment.MatchedRegion);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotReturnAnotherRegionAssignment()
    {
        var sut = CreateSut(
            Adviser("adv-1", organisationId: "org-1", region: "Region B"),
            [Assignment("Region A Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A")]);

        var result = await sut.ResolveAsync(Query("adv-1", ["ContactCentre"]), CancellationToken.None);

        Assert.Empty(result.Assignments);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsOrganisationWideAssignmentForAnyRegionInOrganisation()
    {
        var sut = CreateSut(
            Adviser("adv-1", organisationId: "org-1", region: "Region B"),
            [Assignment("Org Contact Centre", "ContactCentre", organisationId: "org-1")]);

        var result = await sut.ResolveAsync(Query("adv-1", ["ContactCentre"]), CancellationToken.None);

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal("Org Contact Centre", assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.Organisation, assignment.MatchLevel);
        Assert.Equal("org-1", assignment.MatchedOrganisationId);
        Assert.Null(assignment.MatchedRegion);
    }

    [Fact]
    public async Task ResolveAsync_AdviserSpecificAssignmentBeatsOrganisationRegionAssignment()
    {
        var sut = CreateSut(
            Adviser("adv-1", organisationId: "org-1", region: "Region A"),
            [
                Assignment("Regional Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A", priority: 1),
                Assignment("Adviser Contact Centre", "ContactCentre", adviserId: "adv-1", priority: 100)
            ]);

        var result = await sut.ResolveAsync(Query("adv-1", ["ContactCentre"]), CancellationToken.None);

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal("Adviser Contact Centre", assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.Adviser, assignment.MatchLevel);
        Assert.Equal("adv-1", assignment.MatchedAdviserId);
    }

    [Fact]
    public async Task ResolveAsync_OrganisationRegionAssignmentBeatsOrganisationOnlyAssignment()
    {
        var sut = CreateSut(
            Adviser("adv-1", organisationId: "org-1", region: "Region A"),
            [
                Assignment("Org Contact Centre", "ContactCentre", organisationId: "org-1", priority: 1),
                Assignment("Regional Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A", priority: 100)
            ]);

        var result = await sut.ResolveAsync(Query("adv-1", ["ContactCentre"]), CancellationToken.None);

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal("Regional Contact Centre", assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.OrganisationRegion, assignment.MatchLevel);
    }

    [Fact]
    public async Task ResolveAsync_ExcludesDisabledAssignments()
    {
        var sut = CreateSut(
            Adviser("adv-1", organisationId: "org-1", region: "Region A"),
            [Assignment("Disabled Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A", isEnabled: false)]);

        var result = await sut.ResolveAsync(Query("adv-1", ["ContactCentre"]), CancellationToken.None);

        Assert.Empty(result.Assignments);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsFallbackWhenRequestedAndNoSpecificMatchExists()
    {
        var sut = CreateSut(
            Adviser("adv-1", organisationId: "org-1", region: "Region A"),
            [Assignment("Fallback Team", "Fallback", priority: 10)]);

        var result = await sut.ResolveAsync(Query("adv-1", ["ContactCentre", "Fallback"]), CancellationToken.None);

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal("Fallback Team", assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.Fallback, assignment.MatchLevel);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotReturnFallbackWhenSpecificMatchExists()
    {
        var sut = CreateSut(
            Adviser("adv-1", organisationId: "org-1", region: "Region A"),
            [
                Assignment("Regional Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A"),
                Assignment("Fallback Team", "Fallback")
            ]);

        var result = await sut.ResolveAsync(Query("adv-1", ["ContactCentre", "Fallback"]), CancellationToken.None);

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal("Regional Contact Centre", assignment.DisplayName);
        Assert.NotEqual(OrganisationAssignmentMatchLevels.Fallback, assignment.MatchLevel);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNotFoundWhenAdviserDoesNotExist()
    {
        var sut = CreateSut(adviser: null, [Assignment("Fallback Team", "Fallback")]);

        var result = await sut.ResolveAsync(Query("missing-adviser", ["Fallback"]), CancellationToken.None);

        Assert.Equal(AdviserScopedOrganisationAssignmentResolutionStatus.AdviserNotFound, result.Status);
        Assert.Equal("ADVISER_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task ResolveAsync_MissingOrganisationAndRegionStillReturnsAdviserSpecificOrFallback()
    {
        var sut = CreateSut(
            Adviser("adv-1", organisationId: null, region: null),
            [
                Assignment("Org Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A"),
                Assignment("Adviser Contact Centre", "ContactCentre", adviserId: "adv-1"),
                Assignment("Fallback Team", "Fallback")
            ]);

        var result = await sut.ResolveAsync(Query("adv-1", ["ContactCentre", "Fallback"]), CancellationToken.None);

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal("Adviser Contact Centre", assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.Adviser, assignment.MatchLevel);
    }

    private static AdviserScopedOrganisationAssignmentResolver CreateSut(
        Entities.Adviser? adviser,
        IReadOnlyList<OrganisationAssignment> assignments)
    {
        var advisers = new Mock<IAdviserReferenceCacheRepository>();
        advisers.Setup(x => x.GetAllAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) =>
                adviser is not null && ids.Contains(adviser.AdviserId, StringComparer.OrdinalIgnoreCase)
                    ? [adviser]
                    : []);

        var directory = new Mock<IOrganisationAssignmentDirectory>();
        directory.Setup(x => x.ResolveScopedAsync(It.IsAny<OrganisationAssignmentScopedSearch>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrganisationAssignmentScopedSearch search, CancellationToken _) =>
                ResolveScoped(search, assignments));

        return new AdviserScopedOrganisationAssignmentResolver(
            advisers.Object,
            directory.Object,
            NullLogger<AdviserScopedOrganisationAssignmentResolver>.Instance);
    }

    private static IReadOnlyList<OrganisationAssignmentScopedMatch> ResolveScoped(
        OrganisationAssignmentScopedSearch search,
        IReadOnlyList<OrganisationAssignment> assignments)
    {
        var types = search.AssignmentTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (search.IncludeFallback)
            types.Add("Fallback");

        var matches = assignments
            .Where(x => string.Equals(x.Context, search.Context, StringComparison.OrdinalIgnoreCase))
            .Where(x => types.Count == 0 || types.Contains(x.AssignmentType))
            .Where(x => search.IncludeDisabled || x.IsEnabled)
            .Select(x => TryMatch(search, x))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();

        var specific = matches
            .Where(x => !string.Equals(x.Assignment.AssignmentType, "Fallback", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (specific.Length > 0)
            return BestRankPerAssignmentType(specific);

        return search.IncludeFallback
            ? matches.Where(x => string.Equals(x.Assignment.AssignmentType, "Fallback", StringComparison.OrdinalIgnoreCase)).ToArray()
            : [];
    }

    private static IReadOnlyList<OrganisationAssignmentScopedMatch> BestRankPerAssignmentType(
        IReadOnlyList<OrganisationAssignmentScopedMatch> matches)
        => matches
            .GroupBy(x => x.Assignment.AssignmentType, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group =>
            {
                var rank = group.Min(x => x.Rank);
                return group.Where(x => x.Rank == rank);
            })
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Assignment.Priority)
            .ThenBy(x => x.Assignment.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static OrganisationAssignmentScopedMatch? TryMatch(
        OrganisationAssignmentScopedSearch search,
        OrganisationAssignment assignment)
    {
        if (!string.IsNullOrWhiteSpace(assignment.AdviserId) &&
            string.Equals(assignment.AdviserId, search.AdviserId, StringComparison.OrdinalIgnoreCase))
        {
            return new OrganisationAssignmentScopedMatch(
                assignment,
                OrganisationAssignmentMatchLevels.Adviser,
                MatchedOrganisationId: null,
                MatchedRegion: null,
                MatchedAdviserId: search.AdviserId,
                Rank: 1);
        }

        if (!string.IsNullOrWhiteSpace(search.OrganisationId) &&
            !string.IsNullOrWhiteSpace(search.Region) &&
            string.Equals(assignment.OrganisationId, search.OrganisationId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(assignment.Region, search.Region, StringComparison.OrdinalIgnoreCase))
        {
            return new OrganisationAssignmentScopedMatch(
                assignment,
                OrganisationAssignmentMatchLevels.OrganisationRegion,
                MatchedOrganisationId: search.OrganisationId,
                MatchedRegion: search.Region,
                MatchedAdviserId: null,
                Rank: 2);
        }

        if (!string.IsNullOrWhiteSpace(search.OrganisationId) &&
            string.Equals(assignment.OrganisationId, search.OrganisationId, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(assignment.Region))
        {
            return new OrganisationAssignmentScopedMatch(
                assignment,
                OrganisationAssignmentMatchLevels.Organisation,
                MatchedOrganisationId: search.OrganisationId,
                MatchedRegion: null,
                MatchedAdviserId: null,
                Rank: 3);
        }

        if (!string.IsNullOrWhiteSpace(search.Region) &&
            string.IsNullOrWhiteSpace(assignment.OrganisationId) &&
            string.Equals(assignment.Region, search.Region, StringComparison.OrdinalIgnoreCase))
        {
            return new OrganisationAssignmentScopedMatch(
                assignment,
                OrganisationAssignmentMatchLevels.Region,
                MatchedOrganisationId: null,
                MatchedRegion: search.Region,
                MatchedAdviserId: null,
                Rank: 4);
        }

        if (search.IncludeFallback &&
            string.Equals(assignment.AssignmentType, "Fallback", StringComparison.OrdinalIgnoreCase))
        {
            return new OrganisationAssignmentScopedMatch(
                assignment,
                OrganisationAssignmentMatchLevels.Fallback,
                MatchedOrganisationId: null,
                MatchedRegion: null,
                MatchedAdviserId: null,
                Rank: 5);
        }

        return null;
    }

    private static AdviserScopedOrganisationAssignmentQuery Query(string adviserId, IReadOnlyList<string> assignmentTypes)
        => new(adviserId, "Booking", assignmentTypes);

    private static Entities.Adviser Adviser(string adviserId, string? organisationId, string? region)
        => new()
        {
            AdviserId = adviserId,
            DisplayName = adviserId,
            MailboxUserId = adviserId,
            HomePostcode = "S1 1AB",
            BaseOfficeId = organisationId,
            Region = region ?? string.Empty,
            IsActive = true,
            IsBookable = true
        };

    private static OrganisationAssignment Assignment(
        string displayName,
        string assignmentType,
        string? organisationId = null,
        string? region = null,
        string? adviserId = null,
        bool isEnabled = true,
        int priority = 100)
        => new(
            Guid.NewGuid(),
            "Booking",
            assignmentType,
            organisationId,
            ClientId: null,
            region,
            adviserId,
            displayName,
            $"{displayName.Replace(" ", ".", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}@example.test",
            MobileNumber: null,
            Channels: ["Email"],
            isEnabled,
            priority,
            DateTime.UtcNow,
            UpdatedUtc: null);
}
