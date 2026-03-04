using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Services.Common;

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