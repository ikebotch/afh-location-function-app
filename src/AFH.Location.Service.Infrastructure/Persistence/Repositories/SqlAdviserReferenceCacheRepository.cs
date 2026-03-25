using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Domain.Entities;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

public sealed class SqlAdviserReferenceCacheRepository : IAdviserReferenceCacheRepository
{
    private readonly LocationPolicyDbContext _db;

    public SqlAdviserReferenceCacheRepository(LocationPolicyDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
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

    public async Task UpsertAsync(IReadOnlyCollection<Adviser> advisers, DateTime syncedUtc, CancellationToken ct)
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
            row.MailboxUserId = adviser.MailboxUserId;
            row.HomePostcode = adviser.HomePostcode;
            row.Region = adviser.Region;
            row.BaseOfficeId = adviser.BaseOfficeId;
            row.TeamName = adviser.TeamName;
            row.ManagerId = adviser.ManagerId;
            row.SkillsCsv = string.Join('|', adviser.Skills);
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

    private static Adviser Map(AdviserReferenceCacheEntity entity)
    {
        return new Adviser
        {
            AdviserId = entity.AdviserId,
            DisplayName = entity.DisplayName,
            MailboxUserId = entity.MailboxUserId,
            HomePostcode = entity.HomePostcode,
            Region = entity.Region,
            BaseOfficeId = entity.BaseOfficeId,
            TeamName = entity.TeamName,
            ManagerId = entity.ManagerId,
            Skills = string.IsNullOrWhiteSpace(entity.SkillsCsv)
                ? Array.Empty<string>()
                : entity.SkillsCsv.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Rating = entity.Rating,
            IsActive = entity.IsActive,
            IsBookable = entity.IsBookable,
            CoverageRadiusMiles = entity.CoverageRadiusMiles,
            MaxTravelTimeMinutes = entity.MaxTravelTimeMinutes,
            LastSyncedUtc = entity.LastSyncedUtc
        };
    }
}
