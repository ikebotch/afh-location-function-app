using AFH.Location.Service.Domain;

namespace AFH.Location.Service.Application.Abstractions;

public interface IRouteMatrixPolicyProvider
{
    Task<RouteMatrixPolicy> GetAsync(CancellationToken ct);
}