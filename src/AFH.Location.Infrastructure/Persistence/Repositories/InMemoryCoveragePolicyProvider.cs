using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Domain;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class InMemoryCoveragePolicyProvider : ICoveragePolicyProvider
{
    private readonly IConfiguration _configuration;

    public InMemoryCoveragePolicyProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<CoveragePolicy> GetAsync(CancellationToken ct)
    {
        var policy = new CoveragePolicy
        {
            DefaultRadiusMiles = _configuration.GetValue<double?>("LocationSearch:Coverage:DefaultRadiusMiles") ?? 100
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

        foreach (var child in _configuration.GetSection("LocationSearch:Coverage:OfficeRadiusMiles").GetChildren())
        {
            if (double.TryParse(child.Value, out var value))
                policy.OfficeRadiusMiles[child.Key] = value;
        }

        return Task.FromResult(policy);
    }
}
