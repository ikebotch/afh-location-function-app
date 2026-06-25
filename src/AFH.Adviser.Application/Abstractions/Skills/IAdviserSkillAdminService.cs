using AFH.Adviser.Application.Models.Skills;

namespace AFH.Adviser.Application.Abstractions.Skills;

public interface IAdviserSkillAdminService
{
    Task<IReadOnlyList<AdviserSkillCatalogItem>> ListAsync(bool includeInactive, CancellationToken ct);
    Task<AdviserSkillCatalogItem> UpsertAsync(Guid? id, AdviserSkillUpsert upsert, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}
