using AFH.Adviser.Application.Models.Skills;

namespace AFH.Adviser.Application.Abstractions.Skills;

public interface IAdviserSkillCatalogService
{
    Task<AdviserSkillCatalogResult> GetSkillsAsync(CancellationToken ct);
}
