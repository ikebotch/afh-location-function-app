using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Application.Models.V1.Travel;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AFH.Location.Application.Services.V1.Travel;

public sealed class RouteTimeService : IRouteTimeService
{
    private const string DestinationKey = "destination";

    private readonly IRouteMatrixService _routeMatrix;
    private readonly ILogger<RouteTimeService> _logger;

    public RouteTimeService(
        IRouteMatrixService routeMatrix,
        ILogger<RouteTimeService> logger)
    {
        _routeMatrix = routeMatrix;
        _logger = logger;
    }

    public async Task<RouteTimeResult> CalculateAsync(RouteTimeRequest request, CancellationToken ct)
    {
        var started = Stopwatch.StartNew();

        try
        {
            var routes = await _routeMatrix.GetOneToManyAsync(
                (request.Source.Latitude, request.Source.Longitude),
                new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
                {
                    [DestinationKey] = (request.Destination.Latitude, request.Destination.Longitude)
                },
                request.DepartAt,
                ct);

            started.Stop();

            if (!routes.TryGetValue(DestinationKey, out var route) || IsUnavailable(route))
            {
                _logger.LogWarning(
                    "Location route-time unavailable. CorrelationId={CorrelationId} DepartAt={DepartAt} DurationMs={DurationMs}",
                    request.CorrelationId,
                    request.DepartAt,
                    started.ElapsedMilliseconds);

                return new RouteTimeResult
                {
                    CorrelationId = request.CorrelationId,
                    Status = RouteTimeStatus.RouteUnavailable,
                    Warnings =
                    [
                        new TravelCoverageWarning("ROUTE_UNAVAILABLE", "No route-time result was available for the requested coordinate pair.")
                    ]
                };
            }

            _logger.LogInformation(
                "Location route-time calculated. CorrelationId={CorrelationId} DepartAt={DepartAt} TravelTimeMinutes={TravelTimeMinutes} TravelDistanceMiles={TravelDistanceMiles} ResolutionSource={ResolutionSource} DurationMs={DurationMs}",
                request.CorrelationId,
                request.DepartAt,
                route.EtaMinutes,
                route.DistanceMiles,
                route.ResolutionSource,
                started.ElapsedMilliseconds);

            return new RouteTimeResult
            {
                CorrelationId = request.CorrelationId,
                TravelTimeMinutes = route.EtaMinutes,
                TravelDistanceMiles = route.DistanceMiles,
                Status = RouteTimeStatus.Succeeded
            };
        }
        catch (Exception ex)
        {
            started.Stop();
            _logger.LogError(
                ex,
                "Location route-time failed. CorrelationId={CorrelationId} DepartAt={DepartAt} DurationMs={DurationMs}",
                request.CorrelationId,
                request.DepartAt,
                started.ElapsedMilliseconds);

            return new RouteTimeResult
            {
                CorrelationId = request.CorrelationId,
                Status = RouteTimeStatus.Failed,
                Warnings =
                [
                    new TravelCoverageWarning("ROUTE_TIME_FAILED", "Route-time evaluation failed.")
                ]
            };
        }
    }

    private static bool IsUnavailable(RouteResult route)
        => route.EtaMinutes <= 0
           && route.DistanceMiles <= 0d
           && string.Equals(route.Confidence, "Low", StringComparison.OrdinalIgnoreCase);
}
