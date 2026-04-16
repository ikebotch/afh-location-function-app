using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Models.V1;
using AFH.Location.Application.Services.Common;
using AFH.Location.Domain;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace AFH.Location.Application.Services.V1;

public sealed class LocationSearchRoutingCoordinator
{
    private readonly IRoutingService _routing;
    private readonly ILogger<LocationSearchRoutingCoordinator> _logger;

    public LocationSearchRoutingCoordinator(
        IRoutingService routing,
        ILogger<LocationSearchRoutingCoordinator> logger)
    {
        _routing = routing;
        _logger = logger;
    }

    internal async Task ComputeNearestOfficeRouteAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        if (ctx.NearestOfficeRoute is not null)
            return;

        if (ctx.AvailabilityPolicy.RequireCalendarAvailability &&
            !ctx.Candidates.Any(c =>
                ctx.AvailabilityById.TryGetValue(c.Adviser.AdviserId, out var a) &&
                a.State == CalendarAvailabilityState.Ok))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ctx.NearestOfficeId)) return;
        if (!ctx.OfficeCoords.TryGetValue(ctx.NearestOfficeId, out var office)) return;
        if (IsZero(office)) return;

        var (destLat, destLng) = ctx.Destination;

        try
        {
            ctx.NearestOfficeRoute = await GetOrCreateRouteAsync(ctx, (destLat, destLng), (office.Lat, office.Lng), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nearest office routing failed OfficeId={OfficeId}", ctx.NearestOfficeId);
        }
    }

    internal async Task<TravelToClientResult> BuildTravelToClientAsync(
        LocationSearchContext ctx,
        AdviserCandidate candidate,
        bool withinCoverage,
        List<string> reasons,
        CancellationToken ct)
    {
        var adviserId = candidate.Adviser.AdviserId;

        if (!withinCoverage)
        {
            _logger.LogInformation(
                "Travel-to-client routing skipped. AdviserId={AdviserId} WithinCoverage={WithinCoverage} MatrixAttempted={MatrixAttempted} DirectFallbackAttempted={DirectFallbackAttempted} AppliedFallback={AppliedFallback}",
                adviserId,
                false,
                false,
                false,
                "UnverifiedTravel");
            return new TravelToClientResult { EtaMinutes = null, DistanceMiles = null, Confidence = "Low" };
        }

        var matrixAttempted = ctx.RoutesToClient.TryGetValue(adviserId, out var route);
        var hasOrigin = ctx.AdviserOrigins.TryGetValue(adviserId, out var routedOrigin);
        if (matrixAttempted && route is not null && route.EtaMinutes > 0)
        {
            reasons.Add("ROUTE_OK");
            _logger.LogInformation(
                "Travel-to-client routing resolved from matrix. AdviserId={AdviserId} OriginLat={OriginLat} OriginLng={OriginLng} DestinationLat={DestinationLat} DestinationLng={DestinationLng} MatrixAttempted={MatrixAttempted} DirectFallbackAttempted={DirectFallbackAttempted} RoutingStatus={RoutingStatus} EtaMinutes={EtaMinutes} DistanceMiles={DistanceMiles} Confidence={Confidence}",
                adviserId,
                hasOrigin ? routedOrigin.Lat : (double?)null,
                hasOrigin ? routedOrigin.Lng : (double?)null,
                ctx.Destination.Lat,
                ctx.Destination.Lng,
                true,
                false,
                "RouteOk",
                route.EtaMinutes,
                route.DistanceMiles,
                route.Confidence);
            return new TravelToClientResult
            {
                EtaMinutes = route.EtaMinutes,
                DistanceMiles = route.DistanceMiles,
                Confidence = route.Confidence
            };
        }

        if (!hasOrigin)
        {
            reasons.Add("ROUTING_UNAVAILABLE");
            _logger.LogWarning(
                "Travel-to-client routing unavailable. AdviserId={AdviserId} DestinationLat={DestinationLat} DestinationLng={DestinationLng} MatrixAttempted={MatrixAttempted} DirectFallbackAttempted={DirectFallbackAttempted} RoutingStatus={RoutingStatus} AppliedFallback={AppliedFallback}",
                adviserId,
                ctx.Destination.Lat,
                ctx.Destination.Lng,
                matrixAttempted,
                false,
                "OriginMissing",
                "UnverifiedTravel");
            return new TravelToClientResult { EtaMinutes = null, DistanceMiles = null, Confidence = "Low" };
        }

        try
        {
            var (destLat, destLng) = ctx.Destination;
            var fallbackRoute = await GetOrCreateRouteAsync(ctx, (routedOrigin.Lat, routedOrigin.Lng), (destLat, destLng), ct);

            if (fallbackRoute.EtaMinutes > 0)
            {
                reasons.Add("ROUTE_FALLBACK_OK");
                _logger.LogInformation(
                    "Travel-to-client routing resolved from direct fallback. AdviserId={AdviserId} OriginLat={OriginLat} OriginLng={OriginLng} DestinationLat={DestinationLat} DestinationLng={DestinationLng} MatrixAttempted={MatrixAttempted} DirectFallbackAttempted={DirectFallbackAttempted} RoutingStatus={RoutingStatus} EtaMinutes={EtaMinutes} DistanceMiles={DistanceMiles} Confidence={Confidence}",
                    adviserId,
                    routedOrigin.Lat,
                    routedOrigin.Lng,
                    destLat,
                    destLng,
                    matrixAttempted,
                    true,
                    "RouteFallbackOk",
                    fallbackRoute.EtaMinutes,
                    fallbackRoute.DistanceMiles,
                    fallbackRoute.Confidence);
                return new TravelToClientResult
                {
                    EtaMinutes = fallbackRoute.EtaMinutes,
                    DistanceMiles = fallbackRoute.DistanceMiles,
                    Confidence = fallbackRoute.Confidence
                };
            }

            reasons.Add("ROUTING_FAILED");
            _logger.LogWarning(
                "Travel-to-client routing returned no usable route. AdviserId={AdviserId} OriginLat={OriginLat} OriginLng={OriginLng} DestinationLat={DestinationLat} DestinationLng={DestinationLng} MatrixAttempted={MatrixAttempted} DirectFallbackAttempted={DirectFallbackAttempted} RoutingStatus={RoutingStatus} ProviderEtaMinutes={ProviderEtaMinutes} ProviderDistanceMiles={ProviderDistanceMiles} AppliedFallback={AppliedFallback}",
                adviserId,
                routedOrigin.Lat,
                routedOrigin.Lng,
                destLat,
                destLng,
                matrixAttempted,
                true,
                "RouteZero",
                fallbackRoute.EtaMinutes,
                fallbackRoute.DistanceMiles,
                "UnverifiedTravel");
            return new TravelToClientResult { EtaMinutes = null, DistanceMiles = null, Confidence = "Low" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Travel-to-client routing failed. AdviserId={AdviserId} OriginLat={OriginLat} OriginLng={OriginLng} DestinationLat={DestinationLat} DestinationLng={DestinationLng} MatrixAttempted={MatrixAttempted} DirectFallbackAttempted={DirectFallbackAttempted} AppliedFallback={AppliedFallback}",
                adviserId,
                routedOrigin.Lat,
                routedOrigin.Lng,
                ctx.Destination.Lat,
                ctx.Destination.Lng,
                matrixAttempted,
                true,
                "UnverifiedTravel");
            reasons.Add("ROUTING_FAILED");
            return new TravelToClientResult { EtaMinutes = null, DistanceMiles = null, Confidence = "Low" };
        }
    }

    internal async Task<TravelToBaseResult> BuildTravelToBaseAsync(
        LocationSearchContext ctx,
        AdviserCandidate candidate,
        List<string> reasons,
        CancellationToken ct)
    {
        var (destLat, destLng) = ctx.Destination;
        var result = new TravelToBaseResult { HomeMinutes = 0, OfficeMinutes = 0 };

        if (TryGetPositivePrecomputedRoute(ctx.RoutesToHomeByAdviserId, candidate.Adviser.AdviserId, out var precomputedHomeRoute))
        {
            result.HomeMinutes = precomputedHomeRoute.EtaMinutes;
        }
        else if (ctx.AdviserOrigins.TryGetValue(candidate.Adviser.AdviserId, out var home))
        {
            result.HomeMinutes = await SafeEtaAsync(ctx, (destLat, destLng), (home.Lat, home.Lng), "BASE_HOME", reasons, ct);
        }

        var baseOfficeId = ResolveBaseOfficeId(candidate.Adviser.Region, ctx.BaseOfficePolicy);
        if (!string.IsNullOrWhiteSpace(baseOfficeId) &&
            ctx.OfficeCoords.TryGetValue(baseOfficeId, out var office) &&
            !IsZero(office))
        {
            if (TryGetPositivePrecomputedRoute(ctx.RoutesToOfficeByOfficeId, baseOfficeId, out var precomputedOfficeRoute))
            {
                result.OfficeMinutes = precomputedOfficeRoute.EtaMinutes;
                ctx.OfficeRouteMinutesByOfficeId.TryAdd(baseOfficeId, precomputedOfficeRoute.EtaMinutes);
            }
            else
            {
                if (!ctx.OfficeRouteMinutesByOfficeId.TryGetValue(baseOfficeId, out var officeMinutes))
                {
                    officeMinutes = await SafeEtaAsync(ctx, (destLat, destLng), (office.Lat, office.Lng), "BASE_OFFICE", reasons, ct);
                    ctx.OfficeRouteMinutesByOfficeId.TryAdd(baseOfficeId, officeMinutes);
                }

                result.OfficeMinutes = officeMinutes;
            }
        }
        else
        {
            reasons.Add("BASE_OFFICE_UNKNOWN");
        }

        return result;
    }

    private async Task<int> SafeEtaAsync(
        LocationSearchContext ctx,
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        string tag,
        List<string> reasons,
        CancellationToken ct)
    {
        try
        {
            var route = await GetOrCreateRouteAsync(ctx, origin, destination, ct);
            if (route.EtaMinutes > 0) return route.EtaMinutes;

            reasons.Add($"{tag}_ROUTE_ZERO");
            return 0;
        }
        catch
        {
            reasons.Add($"{tag}_ROUTE_FAILED");
            return 0;
        }
    }

    private static bool TryGetPositivePrecomputedRoute(
        IReadOnlyDictionary<string, RouteResult> routes,
        string key,
        out RouteResult route)
    {
        if (routes.TryGetValue(key, out var foundRoute) && foundRoute is not null && foundRoute.EtaMinutes > 0)
        {
            route = foundRoute;
            return true;
        }

        route = default!;
        return false;
    }

    private Task<RouteResult> GetOrCreateRouteAsync(
        LocationSearchContext ctx,
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        CancellationToken ct)
    {
        var key = BuildRouteLookupKey(origin, destination);
        var lazy = ctx.RouteLookupsByPath.GetOrAdd(
            key,
            _ => new Lazy<Task<RouteResult>>(
                () => _routing.GetRouteAsync(origin, destination, ct),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return AwaitMemoizedRouteAsync(ctx, key, lazy);
    }

    private static string BuildRouteLookupKey((double Lat, double Lng) origin, (double Lat, double Lng) destination)
        => $"{origin.Lat:F6}:{origin.Lng:F6}->{destination.Lat:F6}:{destination.Lng:F6}";

    private static async Task<RouteResult> AwaitMemoizedRouteAsync(
        LocationSearchContext ctx,
        string key,
        Lazy<Task<RouteResult>> lazy)
    {
        try
        {
            return await lazy.Value;
        }
        catch
        {
            ctx.RouteLookupsByPath.TryRemove(key, out _);
            throw;
        }
    }

    private static string ResolveBaseOfficeId(string region, BaseOfficePolicy policy)
    {
        if (policy.RegionOfficeMap.TryGetValue(region, out var officeId) && !string.IsNullOrWhiteSpace(officeId))
            return officeId;

        return policy.DefaultOfficeId;
    }

    private static bool IsZero((double Lat, double Lng) value) => value.Lat == 0d && value.Lng == 0d;
}
