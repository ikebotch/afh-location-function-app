using AFH.Location.Application.Models.Travel;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Abstractions.Travel;

using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AFH.Location.Application.Services.Travel;

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

            if (!routes.TryGetValue(DestinationKey, out var route))
            {
                _logger.LogWarning(
                    "Location route-time unavailable. CorrelationId={CorrelationId} DepartAt={DepartAt} DurationMs={DurationMs}",
                    request.CorrelationId,
                    request.DepartAt,
                    started.ElapsedMilliseconds);

                return RouteUnavailable(request.CorrelationId);
            }

            if (IsUnavailable(route))
            {
                if (ProximateRouteFallback.TryEstimate(request.Source, request.Destination, out var fallback))
                {
                    _logger.LogInformation(
                        "Location route-time resolved using proximate fallback. CorrelationId={CorrelationId} DepartAt={DepartAt} TravelTimeMinutes={TravelTimeMinutes} TravelDistanceMiles={TravelDistanceMiles} DurationMs={DurationMs}",
                        request.CorrelationId,
                        request.DepartAt,
                        fallback.TravelTimeMinutes,
                        fallback.DistanceMiles,
                        started.ElapsedMilliseconds);

                    return new RouteTimeResult
                    {
                        CorrelationId = request.CorrelationId,
                        TravelTimeMinutes = fallback.TravelTimeMinutes,
                        TravelDistanceMiles = fallback.DistanceMiles,
                        Status = RouteTimeStatus.Succeeded,
                        Warnings =
                        [
                            new TravelCoverageWarning(
                                "PROXIMATE_ROUTE_FALLBACK",
                                "Route provider returned no route, but the coordinate pair is within the local proximity fallback threshold.")
                        ]
                    };
                }

                _logger.LogWarning(
                    "Location route-time unavailable. CorrelationId={CorrelationId} DepartAt={DepartAt} DurationMs={DurationMs}",
                    request.CorrelationId,
                    request.DepartAt,
                    started.ElapsedMilliseconds);

                return RouteUnavailable(request.CorrelationId);
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

    private static RouteTimeResult RouteUnavailable(string? correlationId)
        => new()
        {
            CorrelationId = correlationId,
            Status = RouteTimeStatus.RouteUnavailable,
            Warnings =
            [
                new TravelCoverageWarning("ROUTE_UNAVAILABLE", "No route-time result was available for the requested coordinate pair.")
            ]
        };
}
