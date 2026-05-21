using AFH.Adviser.Application.Models.Skills;
using AFH.Adviser.Application.Abstractions.Skills;
using AFH.Adviser.Application.Abstractions.Repositories;

namespace AFH.Adviser.Application.Services.Skills;

public sealed class AdviserSkillCatalogService : IAdviserSkillCatalogService
{
    private readonly IAdviserRepository _adviserRepository;

    public AdviserSkillCatalogService(IAdviserRepository adviserRepository)
    {
        _adviserRepository = adviserRepository;
    }

    public async Task<AdviserSkillCatalogResult> GetSkillsAsync(CancellationToken ct)
    {
        var advisers = await _adviserRepository.GetAllAsync(null, ct);

        return new AdviserSkillCatalogResult
        {
            Skills = advisers
                .Where(a => a.IsActive)
                .SelectMany(a => a.Skills)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }
}
