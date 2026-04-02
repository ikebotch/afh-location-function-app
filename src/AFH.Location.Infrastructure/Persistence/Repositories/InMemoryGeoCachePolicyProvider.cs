using AFH.Location.Application.Abstractions;
using AFH.Location.Domain;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class InMemoryGeoCachePolicyProvider : IGeoCachePolicyProvider
{
    private static readonly GeoCachePolicy _policy = new()
    {
        // pick sensible defaults
        SuccessTtl = TimeSpan.FromDays(7),
        FailureTtl = TimeSpan.FromMinutes(30)
    };

    public Task<GeoCachePolicy> GetAsync(CancellationToken ct)
    {
        // Guard: never return zero/negative
        if (_policy.SuccessTtl <= TimeSpan.Zero)
            _policy.SuccessTtl = TimeSpan.FromDays(7);

        if (_policy.FailureTtl <= TimeSpan.Zero)
            _policy.FailureTtl = TimeSpan.FromMinutes(30);

        return Task.FromResult(_policy);
    }
}
