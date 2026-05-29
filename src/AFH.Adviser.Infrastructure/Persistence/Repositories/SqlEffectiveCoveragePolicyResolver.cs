using AFH.Adviser.Application.Abstractions.Feed;
using AFH.Adviser.Application.Models.Feed;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using Microsoft.EntityFrameworkCore;
using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Infrastructure.Persistence.Repositories;

public sealed class SqlEffectiveCoveragePolicyResolver : IEffectiveCoveragePolicyResolver
{
    private readonly LocationPolicyDbContext _db;

    public SqlEffectiveCoveragePolicyResolver(LocationPolicyDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<string, EffectiveCoveragePolicy>> ResolveAsync(
        IReadOnlyCollection<Entities.Adviser> advisers,
        CancellationToken ct)
    {
        if (advisers.Count == 0)
            return new Dictionary<string, EffectiveCoveragePolicy>(StringComparer.OrdinalIgnoreCase);

        var defaultPolicy = await _db.CoverageDefaults
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(ct);

        var regions = advisers
            .Select(x => x.Region)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var adviserIds = advisers
            .Select(x => x.AdviserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var regionPolicyRows = await _db.CoverageRegions
            .AsNoTracking()
            .ToListAsync(ct);
        var regionSet = regions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var regionPolicies = regionPolicyRows
            .Where(x => regionSet.Contains(x.Region))
            .ToDictionary(x => x.Region, StringComparer.OrdinalIgnoreCase);

        var adviserPolicyRows = await _db.CoverageAdvisers
            .AsNoTracking()
            .ToListAsync(ct);
        var adviserIdSet = adviserIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var adviserPolicies = adviserPolicyRows
            .Where(x => adviserIdSet.Contains(x.AdviserId))
            .ToDictionary(x => x.AdviserId, StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, EffectiveCoveragePolicy>(StringComparer.OrdinalIgnoreCase);
        foreach (var adviser in advisers)
        {
            double? radiusMiles = PositiveOrNull(defaultPolicy?.DefaultRadiusMiles);
            int? maxTravelTimeMinutes = PositiveOrNull(defaultPolicy?.DefaultMaxTravelTimeMinutes);
            var radiusSource = radiusMiles.HasValue ? "OrganisationDefault" : "None";

            if (!string.IsNullOrWhiteSpace(adviser.Region) &&
                regionPolicies.TryGetValue(adviser.Region.Trim(), out var regionPolicy))
            {
                if (PositiveOrNull(regionPolicy.RadiusMiles) is { } regionRadius)
                {
                    radiusMiles = regionRadius;
                    radiusSource = "Region";
                }

                if (PositiveOrNull(regionPolicy.MaxTravelTimeMinutes) is { } regionMaxTravel)
                    maxTravelTimeMinutes = regionMaxTravel;
            }

            if (PositiveOrNull(adviser.CoverageRadiusMiles) is { } adviserRadius)
            {
                radiusMiles = adviserRadius;
                radiusSource = "Adviser";
            }

            if (PositiveOrNull(adviser.MaxTravelTimeMinutes) is { } adviserMaxTravel)
                maxTravelTimeMinutes = adviserMaxTravel;

            if (adviserPolicies.TryGetValue(adviser.AdviserId, out var adviserPolicy))
            {
                if (PositiveOrNull(adviserPolicy.RadiusMiles) is { } policyRadius)
                {
                    radiusMiles = policyRadius;
                    radiusSource = "Adviser";
                }

                if (PositiveOrNull(adviserPolicy.MaxTravelTimeMinutes) is { } policyMaxTravel)
                    maxTravelTimeMinutes = policyMaxTravel;
            }

            result[adviser.AdviserId] = new EffectiveCoveragePolicy(
                radiusMiles,
                maxTravelTimeMinutes,
                radiusSource);
        }

        return result;
    }

    private static double? PositiveOrNull(double? value)
        => value is > 0 ? value : null;

    private static int? PositiveOrNull(int? value)
        => value is > 0 ? value : null;
}