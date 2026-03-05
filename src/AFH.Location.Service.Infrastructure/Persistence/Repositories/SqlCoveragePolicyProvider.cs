using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Services.Common;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

/// <summary>
/// Loads coverage policy from SQL Server with configuration fallback.
/// </summary>
public sealed class SqlCoveragePolicyProvider : ICoveragePolicyProvider
{
    private readonly LocationPolicyDbContext _db;
    private readonly IConfiguration _configuration;

    public SqlCoveragePolicyProvider(LocationPolicyDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<CoveragePolicy> GetAsync(CancellationToken ct)
    {
        var policy = new CoveragePolicy
        {
            DefaultRadiusMiles = _configuration.GetValue<double?>("LocationSearch:Coverage:DefaultRadiusMiles") ?? 100,
            DefaultMaxTravelTimeMinutes = Math.Max(1, _configuration.GetValue<int?>("LocationSearch:Coverage:DefaultMaxTravelTimeMinutes") ?? 90)
        };

        foreach (var child in _configuration.GetSection("LocationSearch:Coverage:AdviserRadiusMiles").GetChildren())
        {
            if (double.TryParse(child.Value, out var value))
                policy.AdviserRadiusMiles[child.Key] = value;
        }

        foreach (var child in _configuration.GetSection("LocationSearch:Coverage:RegionRadiusMiles").GetChildren())
        {
            if (double.TryParse(child.Value, out var value))
                policy.RegionRadiusMiles[child.Key] = value;
        }

        foreach (var child in _configuration.GetSection("LocationSearch:Coverage:AdviserMaxTravelTimeMinutes").GetChildren())
        {
            if (int.TryParse(child.Value, out var value) && value > 0)
                policy.AdviserMaxTravelTimeMinutes[child.Key] = value;
        }

        foreach (var child in _configuration.GetSection("LocationSearch:Coverage:RegionMaxTravelTimeMinutes").GetChildren())
        {
            if (int.TryParse(child.Value, out var value) && value > 0)
                policy.RegionMaxTravelTimeMinutes[child.Key] = value;
        }

        var coverageDefault = await _db.CoverageDefaults
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (coverageDefault is not null)
        {
            policy.DefaultRadiusMiles = coverageDefault.DefaultRadiusMiles;
            policy.DefaultMaxTravelTimeMinutes = Math.Max(1, coverageDefault.DefaultMaxTravelTimeMinutes);
        }

        var regionOverrides = await _db.CoverageRegions
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var row in regionOverrides)
        {
            if (!string.IsNullOrWhiteSpace(row.Region))
            {
                if (row.RadiusMiles is > 0)
                    policy.RegionRadiusMiles[row.Region] = row.RadiusMiles.Value;
                if (row.MaxTravelTimeMinutes is > 0)
                    policy.RegionMaxTravelTimeMinutes[row.Region] = row.MaxTravelTimeMinutes.Value;
            }
        }

        var adviserOverrides = await _db.CoverageAdvisers
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var row in adviserOverrides)
        {
            if (!string.IsNullOrWhiteSpace(row.AdviserId))
            {
                if (row.RadiusMiles is > 0)
                    policy.AdviserRadiusMiles[row.AdviserId] = row.RadiusMiles.Value;
                if (row.MaxTravelTimeMinutes is > 0)
                    policy.AdviserMaxTravelTimeMinutes[row.AdviserId] = row.MaxTravelTimeMinutes.Value;
            }
        }

        return policy;
    }
}
