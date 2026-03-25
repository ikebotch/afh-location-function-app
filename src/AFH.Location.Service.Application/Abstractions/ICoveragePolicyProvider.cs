using AFH.Location.Service.Domain;

namespace AFH.Location.Service.Application.Abstractions;

public interface ICoveragePolicyProvider
{
    Task<CoveragePolicy> GetAsync(CancellationToken ct);
}