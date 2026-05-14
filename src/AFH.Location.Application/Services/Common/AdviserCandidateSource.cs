using AFH.Location.Application.Abstractions.Advisers;
using AFH.Location.Application.Models.V1.Requests;
using AFH.Location.Domain.Entities;
namespace AFH.Location.Application.Services.Common;

public sealed class AdviserCandidateSource
{
    private readonly IAdviserRepository _repo;

    public AdviserCandidateSource(IAdviserRepository repo)
    {
        _repo = repo;
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

        var requiredSkills = req.Filters?.RequiredSkills?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToArray()
            ?? Array.Empty<string>();

        var filtered = all
            .Where(a => a.IsActive)
            .Where(a => !excluded.Contains(a.AdviserId))
            .Where(a => regions.Count == 0 || regions.Contains(a.Region))
            .Where(a => requiredSkills.Length == 0 || HasAllSkills(a.Skills.ToArray(), requiredSkills))
            .Select(a => new AdviserCandidate
            {
                Adviser = a,
                IsPreferred = preferred.Contains(a.AdviserId)
            })
            // ORDER FIRST
            .OrderByDescending(x => x.IsPreferred)
            .ThenBy(x => x.Adviser.DisplayName)
            .ThenBy(x => x.Adviser.AdviserId)
            // CAP LAST
            //.Take(maxCandidates)
            .ToList();

        return filtered;
    }

    private static bool HasAllSkills(string[] adviserSkills, string[] requiredSkills)
    {
        if (adviserSkills is null || adviserSkills.Length == 0) return false;

        var set = new HashSet<string>(adviserSkills, StringComparer.OrdinalIgnoreCase);
        return requiredSkills.All(set.Contains);
    }
}

public sealed class AdviserCandidate
{
    public Adviser Adviser { get; init; } = default!;
    public bool IsPreferred { get; init; }
    public List<string> SourceReasons { get; init; } = new();
}
