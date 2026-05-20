using AFH.Location.Application.Abstractions.Advisers;
using AFH.Location.Application.Common;
using AFH.Location.Application.Models.V1.Requests;
using AFH.Location.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AFH.Location.Application.Services.Common;

public sealed class AdviserCandidateSource
{
    private readonly IAdviserRepository _repo;
    private readonly ILogger<AdviserCandidateSource> _logger;

    public AdviserCandidateSource(
        IAdviserRepository repo,
        ILogger<AdviserCandidateSource> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdviserCandidate>> GetCandidatesAsync(
        LocationSearchRequest req,
        CancellationToken ct)
    {
        var all = await _repo.GetAllAsync(req.Filters?.AdviserIds, ct);

        var preferred = new HashSet<string>(
            req.Filters?.PreferredAdviserIds ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var excluded = new HashSet<string>(
            req.Filters?.ExcludeAdviserIds ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var regions = req.Filters?.Regions?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>();

        var requiredSkillKeys = SkillKeyNormaliser
            .ToSkillKeys(req.Filters?.RequiredSkills)
            .ToArray();

        _logger.LogInformation(
            "Location adviser candidate skill filtering started. RequiredSkillKeys={RequiredSkillKeys} AdviserCountBefore={AdviserCountBefore}",
            requiredSkillKeys.Length == 0 ? "NONE" : string.Join(", ", requiredSkillKeys),
            all.Count);

        foreach (var adviser in all)
        {
            var rawSkills = adviser.Skills ?? [];

            var skillKeys = SkillKeyNormaliser
                .ToSkillKeys(rawSkills)
                .ToArray();

            _logger.LogInformation(
                "Location adviser skills. AdviserId={AdviserId} RawSkills={RawSkills} SkillKeys={SkillKeys}",
                adviser.AdviserId,
                rawSkills.Count == 0 ? "NONE" : string.Join(", ", rawSkills),
                skillKeys.Length == 0 ? "NONE" : string.Join(", ", skillKeys));
        }

        var filtered = all
            .Where(a => a.IsActive)
            .Where(a => !excluded.Contains(a.AdviserId))
            .Where(a => regions.Count == 0 || regions.Contains(a.Region))
            .Where(a => requiredSkillKeys.Length == 0 || HasAllSkillKeys(a.Skills, requiredSkillKeys))
            .Select(a => new AdviserCandidate
            {
                Adviser = a,
                IsPreferred = preferred.Contains(a.AdviserId)
            })
            .OrderByDescending(x => x.IsPreferred)
            .ThenBy(x => x.Adviser.DisplayName)
            .ThenBy(x => x.Adviser.AdviserId)
            .ToList();

        _logger.LogInformation(
            "Location adviser candidate skill filtering complete. AdviserCountAfter={AdviserCountAfter}",
            filtered.Count);

        return filtered;
    }

    private static bool HasAllSkillKeys(
        IEnumerable<string>? adviserSkills,
        IEnumerable<string>? requiredSkillKeys)
    {
        var required = SkillKeyNormaliser.ToSkillKeys(requiredSkillKeys);

        if (required.Count == 0)
            return true;

        var adviserSkillKeys = SkillKeyNormaliser
            .ToSkillKeys(adviserSkills)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return adviserSkillKeys.Count > 0 &&
               required.All(adviserSkillKeys.Contains);
    }
}

