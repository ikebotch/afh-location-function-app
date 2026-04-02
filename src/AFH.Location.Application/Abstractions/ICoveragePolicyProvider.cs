using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions;

public interface ICoveragePolicyProvider
{
    Task<CoveragePolicy> GetAsync(CancellationToken ct);
}