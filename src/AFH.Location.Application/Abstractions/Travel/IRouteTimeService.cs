using AFH.Location.Application.Models.Travel;


namespace AFH.Location.Application.Abstractions.Travel;

public interface IRouteTimeService
{
    Task<RouteTimeResult> CalculateAsync(RouteTimeRequest request, CancellationToken ct);
}
