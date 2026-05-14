using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions.Coverage;

public interface IRouteMatrixPolicyProvider
{
    Task<RouteMatrixPolicy> GetAsync(CancellationToken ct);
}