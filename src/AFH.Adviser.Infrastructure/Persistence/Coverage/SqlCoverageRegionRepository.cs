using AFH.Adviser.Application.Abstractions.Coverage;
using AFH.Adviser.Application.Models.Coverage;
using AFH.Adviser.Infrastructure.Persistence.Coverage.Entities;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Infrastructure.Persistence.Coverage;

public sealed class SqlCoverageRegionRepository : ICoverageRegionRepository
{
    private readonly AdviserDirectoryDbContext _db;

    public SqlCoverageRegionRepository(AdviserDirectoryDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CoverageRegion>> SearchAsync(CoverageRegionSearch search, CancellationToken ct)
    {
        var query = _db.CoverageRegions
            .AsNoTracking()
            .Include(x => x.AdviserAssignments)
            .AsQueryable();

        if (!search.IncludeInactive)
            query = query.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(search.RegionCode))
        {
            var code = search.RegionCode.Trim();
            query = query.Where(x => x.Code == code);
        }

        if (!string.IsNullOrWhiteSpace(search.AdviserId))
        {
            var adviserId = search.AdviserId.Trim();
            query = query.Where(x =>
                x.LeadAdviserId == adviserId ||
                x.AdviserAssignments.Any(a => a.AdviserId == adviserId && a.IsActive));
        }

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(x =>
                x.Code.Contains(term) ||
                x.Name.Contains(term) ||
                (x.LeadAdviserName != null && x.LeadAdviserName.Contains(term)) ||
                x.Postcodes.Contains(term) ||
                x.Skills.Contains(term));
        }

        var rows = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Code)
            .ToArrayAsync(ct);

        return rows.Select(ToModel).ToArray();
    }

    public async Task<CoverageRegion?> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.CoverageRegions
            .AsNoTracking()
            .Include(x => x.AdviserAssignments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return row is null ? null : ToModel(row);
    }

    public async Task<CoverageRegion> CreateAsync(CoverageRegionUpsert request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = new CoverageRegionEntity
        {
            Id = Guid.NewGuid(),
            CreatedUtc = now
        };

        Apply(row, request, now);
        _db.CoverageRegions.Add(row);
        await _db.SaveChangesAsync(ct);
        return ToModel(row);
    }

    public async Task<CoverageRegion?> UpdateAsync(Guid id, CoverageRegionUpsert request, CancellationToken ct)
    {
        var row = await _db.CoverageRegions
            .Include(x => x.AdviserAssignments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (row is null)
            return null;

        Apply(row, request, DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return ToModel(row);
    }

    public async Task<bool> DisableAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.CoverageRegions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
            return false;

        row.IsActive = false;
        row.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.CoverageRegions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
            return false;

        _db.CoverageRegions.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AdviserRegionAssignment> AssignAdviserAsync(AdviserRegionAssignmentUpsert request, CancellationToken ct)
    {
        var region = await _db.CoverageRegions
            .Include(x => x.AdviserAssignments)
            .FirstOrDefaultAsync(x => x.Id == request.RegionId, ct)
            ?? throw new InvalidOperationException("Coverage region was not found.");

        var now = DateTime.UtcNow;
        var adviserId = Require(request.AdviserId, "adviserId");
        var existing = region.AdviserAssignments.FirstOrDefault(x => x.AdviserId == adviserId);

        if (request.IsLead)
        {
            foreach (var assignment in region.AdviserAssignments)
                assignment.IsLead = false;

            region.LeadAdviserId = adviserId;
            region.LeadAdviserName = Require(request.AdviserName, "adviserName");
        }

        var row = existing ?? new AdviserRegionAssignmentEntity
        {
            Id = Guid.NewGuid(),
            RegionId = request.RegionId,
            CreatedUtc = now
        };

        row.AdviserId = adviserId;
        row.AdviserName = Require(request.AdviserName, "adviserName");
        row.Role = TrimToNull(request.Role);
        row.IsLead = request.IsLead;
        row.IsActive = request.IsActive;
        row.UpdatedUtc = now;

        if (existing is null)
            region.AdviserAssignments.Add(row);

        region.UpdatedUtc = now;
        await _db.SaveChangesAsync(ct);
        return ToModel(row, region.Code);
    }

    public async Task<bool> RemoveAdviserAsync(Guid assignmentId, CancellationToken ct)
    {
        var row = await _db.AdviserRegionAssignments
            .Include(x => x.Region)
            .FirstOrDefaultAsync(x => x.Id == assignmentId, ct);

        if (row is null)
            return false;

        if (row.Region is not null && row.IsLead)
        {
            row.Region.LeadAdviserId = null;
            row.Region.LeadAdviserName = null;
            row.Region.UpdatedUtc = DateTime.UtcNow;
        }

        _db.AdviserRegionAssignments.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(CoverageRegionEntity row, CoverageRegionUpsert request, DateTime now)
    {
        row.Code = Require(request.Code, "code");
        row.Name = Require(request.Name, "name");
        row.LeadAdviserId = TrimToNull(request.LeadAdviserId);
        row.LeadAdviserName = TrimToNull(request.LeadAdviserName);
        row.Postcodes = string.Join(',', Normalise(request.Postcodes));
        row.Skills = string.Join(',', Normalise(request.Skills));
        row.CoverageRadiusMiles = request.CoverageRadiusMiles;
        row.MaxTravelTimeMinutes = request.MaxTravelTimeMinutes;
        row.IsActive = request.IsActive;
        row.UpdatedUtc = now;
    }

    private static CoverageRegion ToModel(CoverageRegionEntity row)
        => new(
            row.Id,
            row.Code,
            row.Name,
            row.LeadAdviserId,
            row.LeadAdviserName,
            Split(row.Postcodes),
            Split(row.Skills),
            row.CoverageRadiusMiles,
            row.MaxTravelTimeMinutes,
            row.IsActive,
            row.CreatedUtc,
            row.UpdatedUtc,
            row.AdviserAssignments
                .OrderByDescending(x => x.IsLead)
                .ThenBy(x => x.AdviserName)
                .Select(x => ToModel(x, row.Code))
                .ToArray());

    private static AdviserRegionAssignment ToModel(AdviserRegionAssignmentEntity row, string regionCode)
        => new(
            row.Id,
            row.RegionId,
            regionCode,
            row.AdviserId,
            row.AdviserName,
            row.Role,
            row.IsLead,
            row.IsActive,
            row.CreatedUtc,
            row.UpdatedUtc);

    private static string Require(string? value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{name} is required.", name)
            : value.Trim();

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> Normalise(IReadOnlyList<string>? values)
        => (values ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> Split(string? values)
        => string.IsNullOrWhiteSpace(values)
            ? []
            : values.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
