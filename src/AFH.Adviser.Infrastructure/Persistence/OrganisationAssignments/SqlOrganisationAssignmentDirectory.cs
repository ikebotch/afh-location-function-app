using AFH.Adviser.Application.Abstractions.OrganisationAssignments;
using AFH.Adviser.Application.Models.OrganisationAssignments;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;

public sealed class SqlOrganisationAssignmentDirectory : IOrganisationAssignmentDirectory
{
    private const string FallbackAssignmentType = "Fallback";

    private readonly AdviserDirectoryDbContext _db;

    public SqlOrganisationAssignmentDirectory(AdviserDirectoryDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OrganisationAssignment>> SearchAsync(OrganisationAssignmentSearch search, CancellationToken ct)
    {
        var query = _db.OrganisationAssignments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search.Context))
            query = query.Where(x => x.Context == search.Context.Trim());

        if (search.AssignmentTypes.Count > 0)
        {
            var types = search.AssignmentTypes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            query = query.Where(x => types.Contains(x.AssignmentType));
        }

        if (!string.IsNullOrWhiteSpace(search.OrganisationId))
            query = query.Where(x => x.OrganisationId == null || x.OrganisationId == search.OrganisationId.Trim());

        if (!string.IsNullOrWhiteSpace(search.ClientId))
            query = query.Where(x => x.ClientId == null || x.ClientId == search.ClientId.Trim());

        if (!string.IsNullOrWhiteSpace(search.Region))
            query = query.Where(x => x.Region == null || x.Region == search.Region.Trim());

        if (!string.IsNullOrWhiteSpace(search.AdviserId))
            query = query.Where(x => x.AdviserId == null || x.AdviserId == search.AdviserId.Trim());

        if (!search.IncludeDisabled)
            query = query.Where(x => x.IsEnabled);

        var rows = await query
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.AssignmentType)
            .ThenBy(x => x.DisplayName)
            .ToArrayAsync(ct);

        return rows.Select(ToModel).ToArray();
    }

    public async Task<IReadOnlyList<OrganisationAssignmentScopedMatch>> ResolveScopedAsync(
        OrganisationAssignmentScopedSearch search,
        CancellationToken ct)
    {
        var assignmentTypes = NormaliseAssignmentTypes(search.AssignmentTypes);
        var assignmentTypeSet = assignmentTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (search.IncludeFallback)
            assignmentTypeSet.Add(FallbackAssignmentType);

        if (assignmentTypeSet.Count == 0)
            return [];

        var context = search.Context.Trim();
        var query = _db.OrganisationAssignments.AsNoTracking()
            .Where(x => x.Context == context)
            .Where(x => assignmentTypeSet.Contains(x.AssignmentType));

        if (!search.IncludeDisabled)
            query = query.Where(x => x.IsEnabled);

        var rows = await query
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.AssignmentType)
            .ThenBy(x => x.DisplayName)
            .ToArrayAsync(ct);

        var matches = rows
            .Select(ToModel)
            .Select(x => TryMatch(search, x))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();

        var specificMatches = matches
            .Where(x => !string.Equals(x.Assignment.AssignmentType, FallbackAssignmentType, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (specificMatches.Length > 0)
            return BestRankPerAssignmentType(specificMatches);

        if (!search.IncludeFallback)
            return [];

        return matches
            .Where(x => string.Equals(x.Assignment.AssignmentType, FallbackAssignmentType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Assignment.Priority)
            .ThenBy(x => x.Assignment.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<OrganisationAssignment?> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.OrganisationAssignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToModel(row);
    }

    public async Task<OrganisationAssignment> CreateAsync(OrganisationAssignmentUpsert request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = new OrganisationAssignmentEntity
        {
            Id = Guid.NewGuid(),
            CreatedUtc = now
        };
        Apply(row, request, now);
        _db.OrganisationAssignments.Add(row);
        await _db.SaveChangesAsync(ct);
        return ToModel(row);
    }

    public async Task<OrganisationAssignment?> UpdateAsync(Guid id, OrganisationAssignmentUpsert request, CancellationToken ct)
    {
        var row = await _db.OrganisationAssignments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
            return null;

        Apply(row, request, DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return ToModel(row);
    }

    public async Task<bool> DisableAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.OrganisationAssignments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
            return false;

        row.IsEnabled = false;
        row.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.OrganisationAssignments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
            return false;

        _db.OrganisationAssignments.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(OrganisationAssignmentEntity row, OrganisationAssignmentUpsert request, DateTime now)
    {
        row.Context = Require(request.Context, "context");
        row.AssignmentType = Require(request.AssignmentType, "assignmentType");
        row.OrganisationId = TrimToNull(request.OrganisationId);
        row.ClientId = TrimToNull(request.ClientId);
        row.Region = TrimToNull(request.Region);
        row.AdviserId = TrimToNull(request.AdviserId);
        row.DisplayName = Require(request.DisplayName, "displayName");
        row.Email = TrimToNull(request.Email);
        row.MobileNumber = TrimToNull(request.MobileNumber);
        row.Channels = string.Join(',', NormaliseChannels(request.Channels));
        row.IsEnabled = request.IsEnabled;
        row.Priority = request.Priority;
        row.UpdatedUtc = now;
    }

    private static OrganisationAssignment ToModel(OrganisationAssignmentEntity row)
        => new(
            row.Id,
            row.Context,
            row.AssignmentType,
            row.OrganisationId,
            row.ClientId,
            row.Region,
            row.AdviserId,
            row.DisplayName,
            row.Email,
            row.MobileNumber,
            SplitChannels(row.Channels),
            row.IsEnabled,
            row.Priority,
            row.CreatedUtc,
            row.UpdatedUtc);

    private static IReadOnlyList<OrganisationAssignmentScopedMatch> BestRankPerAssignmentType(
        IReadOnlyList<OrganisationAssignmentScopedMatch> matches)
        => matches
            .GroupBy(x => x.Assignment.AssignmentType, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group =>
            {
                var bestRank = group.Min(x => x.Rank);
                return group.Where(x => x.Rank == bestRank);
            })
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Assignment.Priority)
            .ThenBy(x => x.Assignment.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static OrganisationAssignmentScopedMatch? TryMatch(
        OrganisationAssignmentScopedSearch search,
        OrganisationAssignment assignment)
    {
        var adviserId = TrimToNull(search.AdviserId);
        var organisationId = TrimToNull(search.OrganisationId);
        var region = TrimToNull(search.Region);

        if (!string.IsNullOrWhiteSpace(assignment.AdviserId) &&
            string.Equals(assignment.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase))
        {
            return new OrganisationAssignmentScopedMatch(
                assignment,
                OrganisationAssignmentMatchLevels.Adviser,
                MatchedOrganisationId: null,
                MatchedRegion: null,
                MatchedAdviserId: adviserId,
                Rank: 1);
        }

        if (!string.IsNullOrWhiteSpace(organisationId) &&
            !string.IsNullOrWhiteSpace(region) &&
            string.Equals(assignment.OrganisationId, organisationId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(assignment.Region, region, StringComparison.OrdinalIgnoreCase))
        {
            return new OrganisationAssignmentScopedMatch(
                assignment,
                OrganisationAssignmentMatchLevels.OrganisationRegion,
                MatchedOrganisationId: organisationId,
                MatchedRegion: region,
                MatchedAdviserId: null,
                Rank: 2);
        }

        if (!string.IsNullOrWhiteSpace(organisationId) &&
            string.Equals(assignment.OrganisationId, organisationId, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(assignment.Region))
        {
            return new OrganisationAssignmentScopedMatch(
                assignment,
                OrganisationAssignmentMatchLevels.Organisation,
                MatchedOrganisationId: organisationId,
                MatchedRegion: null,
                MatchedAdviserId: null,
                Rank: 3);
        }

        if (!string.IsNullOrWhiteSpace(region) &&
            string.IsNullOrWhiteSpace(assignment.OrganisationId) &&
            string.Equals(assignment.Region, region, StringComparison.OrdinalIgnoreCase))
        {
            return new OrganisationAssignmentScopedMatch(
                assignment,
                OrganisationAssignmentMatchLevels.Region,
                MatchedOrganisationId: null,
                MatchedRegion: region,
                MatchedAdviserId: null,
                Rank: 4);
        }

        if (search.IncludeFallback &&
            string.Equals(assignment.AssignmentType, FallbackAssignmentType, StringComparison.OrdinalIgnoreCase))
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

    private static string Require(string? value, string field)
        => !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"{field} is required.");

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> SplitChannels(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static IReadOnlyList<string> NormaliseChannels(IReadOnlyList<string> channels)
    {
        var values = channels
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? ["Email"] : values;
    }

    private static IReadOnlyList<string> NormaliseAssignmentTypes(IEnumerable<string> assignmentTypes)
        => assignmentTypes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
