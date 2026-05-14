using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions.Coverage;

public interface ICoveragePolicyProvider
{
    Task<CoveragePolicy> GetAsync(CancellationToken ct);
}