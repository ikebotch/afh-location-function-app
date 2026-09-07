using AFH.Adviser.Application.Services.Skills;
using AFH.Adviser.Application.Abstractions.Repositories;
using Entities = AFH.Adviser.Domain.Entities;

using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Infrastructure.Persistence.Repositories;

public sealed class SqlAdviserReferenceCacheRepository : IAdviserReferenceCacheRepository
{
    private readonly LocationPolicyDbContext _db;

    public SqlAdviserReferenceCacheRepository(LocationPolicyDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
    {
        var query = _db.AdviserReferenceCache.AsQueryable();
        if (adviserIds is { Count: > 0 })
        {
            var ids = adviserIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
            query = query.Where(x => ids.Contains(x.AdviserId));
        }

        var rows = await query.AsNoTracking().ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task UpsertAsync(IReadOnlyCollection<Entities.Adviser> advisers, DateTime syncedUtc, CancellationToken ct)
    {
        var byId = advisers.ToDictionary(x => x.AdviserId, StringComparer.OrdinalIgnoreCase);
        var existing = await _db.AdviserReferenceCache.Where(x => byId.Keys.Contains(x.AdviserId)).ToListAsync(ct);

        foreach (var adviser in advisers)
        {
            var row = existing.FirstOrDefault(x => string.Equals(x.AdviserId, adviser.AdviserId, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new AdviserReferenceCacheEntity { AdviserId = adviser.AdviserId };
                _db.AdviserReferenceCache.Add(row);
            }

            row.DisplayName = adviser.DisplayName;
            row.XPlanAdviserId = string.IsNullOrWhiteSpace(adviser.XPlanAdviserId) ? null : adviser.XPlanAdviserId.Trim();
            row.MailboxUserId = adviser.MailboxUserId;
            row.HomePostcode = adviser.HomePostcode;
            row.Region = adviser.Region;
            row.BaseOfficeId = adviser.BaseOfficeId;
            row.TeamName = adviser.TeamName;
            row.ManagerId = adviser.ManagerId;
            row.SkillsCsv = SkillNormaliser.ToCsv(adviser.Skills);
            row.Rating = adviser.Rating;
            row.IsActive = adviser.IsActive;
            row.IsBookable = adviser.IsBookable;
            row.CoverageRadiusMiles = adviser.CoverageRadiusMiles;
            row.MaxTravelTimeMinutes = adviser.MaxTravelTimeMinutes;
            row.LastSyncedUtc = syncedUtc;
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> HasDataAsync(CancellationToken ct)
        => _db.AdviserReferenceCache.AnyAsync(ct);

    public async Task<bool> SetActiveAsync(string adviserId, bool isActive, CancellationToken ct)
    {
        var normalizedId = adviserId.Trim();
        var row = await _db.AdviserReferenceCache.SingleOrDefaultAsync(x => x.AdviserId == normalizedId, ct);
        if (row is null)
            return false;

        row.IsActive = isActive;
        row.LastSyncedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(string adviserId, CancellationToken ct)
    {
        var normalizedId = adviserId.Trim();
        var row = await _db.AdviserReferenceCache.SingleOrDefaultAsync(x => x.AdviserId == normalizedId, ct);
        if (row is null)
            return false;

        _db.AdviserReferenceCache.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static Entities.Adviser Map(AdviserReferenceCacheEntity entity)
    {
        return new Entities.Adviser
        {
            AdviserId = entity.AdviserId,
            XPlanAdviserId = entity.XPlanAdviserId,
            DisplayName = entity.DisplayName,
            MailboxUserId = entity.MailboxUserId,
            HomePostcode = entity.HomePostcode,
            Region = entity.Region,
            BaseOfficeId = entity.BaseOfficeId,
            TeamName = entity.TeamName,
            ManagerId = entity.ManagerId,
            Skills = SkillNormaliser.FromCsv(entity.SkillsCsv),
            Rating = entity.Rating,
            IsActive = entity.IsActive,
            IsBookable = entity.IsBookable,
            CoverageRadiusMiles = entity.CoverageRadiusMiles,
            MaxTravelTimeMinutes = entity.MaxTravelTimeMinutes,
            LastSyncedUtc = entity.LastSyncedUtc
        };
    }
}
