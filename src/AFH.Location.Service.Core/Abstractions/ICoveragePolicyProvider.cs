using AFH.Location.Service.Core.Services.Common;

namespace AFH.Location.Service.Core.Abstractions;

public interface ICoveragePolicyProvider
{
    Task<CoveragePolicy> GetAsync(CancellationToken ct);
}