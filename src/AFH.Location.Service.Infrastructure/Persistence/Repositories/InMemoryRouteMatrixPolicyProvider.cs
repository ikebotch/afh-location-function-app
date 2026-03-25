using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Domain;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

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
