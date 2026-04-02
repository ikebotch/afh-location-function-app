using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions;

public interface IRouteMatrixPolicyProvider
{
    Task<RouteMatrixPolicy> GetAsync(CancellationToken ct);
}