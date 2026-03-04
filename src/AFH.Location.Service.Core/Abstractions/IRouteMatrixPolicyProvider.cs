using AFH.Location.Service.Core.Services.Common;

namespace AFH.Location.Service.Core.Abstractions;

public interface IRouteMatrixPolicyProvider
{
    Task<RouteMatrixPolicy> GetAsync(CancellationToken ct);
}