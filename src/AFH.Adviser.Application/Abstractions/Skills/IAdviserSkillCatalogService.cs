using AFH.Adviser.Application.Models.Skills;

namespace AFH.Adviser.Application.Abstractions.Skills;

public interface IAdviserSkillCatalogService
{
    Task<AdviserSkillCatalogResult> GetLicensesAsync(CancellationToken ct);
}
