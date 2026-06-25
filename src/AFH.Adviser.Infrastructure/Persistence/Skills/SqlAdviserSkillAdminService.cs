using AFH.Adviser.Application.Abstractions.Skills;
using AFH.Adviser.Application.Models.Skills;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;
using AFH.Adviser.Infrastructure.Persistence.Skills.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Infrastructure.Persistence.Skills;

public sealed class SqlAdviserSkillAdminService : IAdviserSkillAdminService
{
    private readonly AdviserDirectoryDbContext _db;

    public SqlAdviserSkillAdminService(AdviserDirectoryDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdviserSkillCatalogItem>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _db.AdviserSkillCatalog.AsNoTracking();
        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var rows = await query.OrderBy(x => x.Name).ToArrayAsync(ct);
        return rows.Select(ToModel).ToArray();
    }

    public async Task<AdviserSkillCatalogItem> UpsertAsync(Guid? id, AdviserSkillUpsert upsert, CancellationToken ct)
    {
        var normalizedName = upsert.Name.Trim();
        var entity = id is { } skillId
            ? await _db.AdviserSkillCatalog.SingleOrDefaultAsync(x => x.Id == skillId, ct)
            : await _db.AdviserSkillCatalog.SingleOrDefaultAsync(x => x.Name == normalizedName, ct);

        if (entity is null)
        {
            entity = new AdviserSkillCatalogEntity
            {
                Id = id.GetValueOrDefault(Guid.NewGuid()),
                CreatedUtc = DateTime.UtcNow
            };
            _db.AdviserSkillCatalog.Add(entity);
        }
        else
        {
            entity.UpdatedUtc = DateTime.UtcNow;
        }

        entity.Name = normalizedName;
        entity.Category = Normalize(upsert.Category);
        entity.Description = Normalize(upsert.Description);
        entity.LicenseRequired = upsert.LicenseRequired;
        entity.Certification = Normalize(upsert.Certification);
        entity.RenewalMonths = upsert.RenewalMonths;
        entity.IsActive = upsert.IsActive;

        await _db.SaveChangesAsync(ct);
        return ToModel(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await _db.AdviserSkillCatalog.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return false;

        _db.AdviserSkillCatalog.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdviserSkillCatalogItem ToModel(AdviserSkillCatalogEntity entity)
        => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Category = entity.Category,
            Description = entity.Description,
            LicenseRequired = entity.LicenseRequired,
            Certification = entity.Certification,
            RenewalMonths = entity.RenewalMonths,
            IsActive = entity.IsActive
        };
}
