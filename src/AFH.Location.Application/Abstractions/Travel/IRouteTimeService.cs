using AFH.Location.Application.Travel;

namespace AFH.Location.Application.Abstractions.Travel;

public interface IRouteTimeService
{
    Task<RouteTimeResult> CalculateAsync(RouteTimeRequest request, CancellationToken ct);
}
