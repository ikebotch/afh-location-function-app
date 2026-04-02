using AFH.Location.Application.Abstractions;
using AFH.Location.Domain;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class InMemoryRouteMatrixPolicyProvider : IRouteMatrixPolicyProvider
{
    private static readonly RouteMatrixPolicy Policy = new()
    {
        MaxOriginsPerCall = 50,
        SuccessConfidence = "High",
        FailureConfidence = "Low"
    };

    public Task<RouteMatrixPolicy> GetAsync(CancellationToken ct) => Task.FromResult(Policy);
}
