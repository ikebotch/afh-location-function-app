using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Application.Models.V1.Travel;
using AFH.Location.Domain.Travel;
using Microsoft.Extensions.Logging;

namespace AFH.Location.Application.Services.V1.Travel;

public sealed class TravelCoverageService : ITravelCoverageService
{
    private readonly IPostcodeCoordinateResolver _coordinateResolver;
    private readonly ITravelRouteOutcomeProvider _routeOutcomeProvider;
    private readonly ILogger<TravelCoverageService> _logger;

    public TravelCoverageService(
        IPostcodeCoordinateResolver coordinateResolver,
        ITravelRouteOutcomeProvider routeOutcomeProvider,
        ILogger<TravelCoverageService> logger)
    {
        _coordinateResolver = coordinateResolver;
        _routeOutcomeProvider = routeOutcomeProvider;
        _logger = logger;
    }

    public async Task<TravelCoverageResult> EvaluateAsync(TravelCoverageRequest request, CancellationToken ct)
    {
        var sourcePostcode = NormalisePostcode(request.SourcePostcode);
        var source = await _coordinateResolver.ResolveAsync(sourcePostcode, ct);

        if (!source.Succeeded)
        {
            return new TravelCoverageResult
            {
                SourcePostcode = sourcePostcode,
                TimeContext = request.TimeContext,
                RequestContext = request.RequestContext,
                Destinations = request.Destinations.Select(destination => BuildSourceUnresolved(destination)).ToList()
            };
        }

        var destinationResolutions = await ResolveDestinationsAsync(request.Destinations, ct);
        
        var routeDestinations = new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase);
        var routes = new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in destinationResolutions)
        {
            if (!item.Value.Succeeded)
                continue;

            var destPostcode = NormalisePostcode(item.Value.Postcode);
            var destCoords = item.Value.Coordinates!;

            bool isSameOrigin = string.Equals(sourcePostcode, destPostcode, StringComparison.OrdinalIgnoreCase)
                || (source.Coordinates != null && source.Coordinates.Latitude == destCoords.Latitude && source.Coordinates.Longitude == destCoords.Longitude);

            if (isSameOrigin)
            {
                routes[item.Key] = new TravelRouteOutcome(0, 0d, "High", TravelRouteResolutionSource.Unknown);
            }
            else
            {
                routeDestinations[item.Key] = destCoords;
            }
        }

        if (routeDestinations.Count > 0)
        {
            var providerRoutes = await _routeOutcomeProvider.GetOutcomesAsync(
                new TravelRouteOutcomeRequest
                {
                    Source = source.Coordinates!,
                    Destinations = routeDestinations,
                    TimeContext = request.TimeContext
                },
                ct);

            foreach (var route in providerRoutes)
            {
                routes[route.Key] = route.Value;
            }
        }

        var outcomes = new List<TravelCoverageDestinationOutcome>(request.Destinations.Count);
        foreach (var destination in request.Destinations)
        {
            var key = BuildDestinationKey(destination);
            if (!destinationResolutions.TryGetValue(key, out var resolved) || !resolved.Succeeded)
            {
                outcomes.Add(BuildDestinationUnresolved(destination));
                continue;
            }

            if (!routes.TryGetValue(key, out var route) || !route.HasUsableRoute)
            {
                outcomes.Add(BuildRouteUnavailable(destination, resolved.Coordinates));
                continue;
            }

            var policy = new TravelCoveragePolicy(destination.MaxTravelTimeMinutes, destination.MaxDistanceMiles);
            var decision = policy.Evaluate(route);

            outcomes.Add(new TravelCoverageDestinationOutcome
            {
                CorrelationId = destination.CorrelationId,
                Postcode = NormalisePostcode(destination.Postcode),
                Status = decision.Status == CoverageDecisionStatus.RouteUnavailable
                    ? TravelCoverageStatus.RouteUnavailable
                    : TravelCoverageStatus.Succeeded,
                Coordinates = resolved.Coordinates,
                Route = route,
                Coverage = new TravelCoverageOutcome
                {
                    IsWithinCoverage = decision.IsWithinCoverage,
                    MaxTravelTimeMinutes = destination.MaxTravelTimeMinutes,
                    MaxDistanceMiles = destination.MaxDistanceMiles
                }
            });
        }

        _logger.LogInformation(
            "Travel coverage evaluation complete. CorrelationId={CorrelationId} DestinationCount={DestinationCount} RouteDestinationCount={RouteDestinationCount}",
            request.RequestContext.CorrelationId,
            outcomes.Count,
            routeDestinations.Count);

        return new TravelCoverageResult
        {
            SourcePostcode = sourcePostcode,
            SourceCoordinates = source.Coordinates,
            TimeContext = request.TimeContext,
            Destinations = outcomes,
            RequestContext = request.RequestContext
        };
    }

    private async Task<Dictionary<string, PostcodeCoordinateResolution>> ResolveDestinationsAsync(
        IReadOnlyList<TravelCoverageDestinationRequest> destinations,
        CancellationToken ct)
    {
        var resolutions = new Dictionary<string, PostcodeCoordinateResolution>(StringComparer.OrdinalIgnoreCase);
        foreach (var destination in destinations)
        {
            var key = BuildDestinationKey(destination);
            if (resolutions.ContainsKey(key))
                continue;

            resolutions[key] = await _coordinateResolver.ResolveAsync(destination.Postcode, ct);
        }

        return resolutions;
    }

    private static TravelCoverageDestinationOutcome BuildSourceUnresolved(TravelCoverageDestinationRequest destination)
    {
        return new TravelCoverageDestinationOutcome
        {
            CorrelationId = destination.CorrelationId,
            Postcode = NormalisePostcode(destination.Postcode),
            Status = TravelCoverageStatus.SourcePostcodeUnresolved,
            Warnings =
            [
                new TravelCoverageWarning("SOURCE_POSTCODE_UNRESOLVED", "Source postcode could not be resolved to coordinates.")
            ]
        };
    }

    private static TravelCoverageDestinationOutcome BuildDestinationUnresolved(TravelCoverageDestinationRequest destination)
    {
        return new TravelCoverageDestinationOutcome
        {
            CorrelationId = destination.CorrelationId,
            Postcode = NormalisePostcode(destination.Postcode),
            Status = TravelCoverageStatus.DestinationPostcodeUnresolved,
            Warnings =
            [
                new TravelCoverageWarning("DESTINATION_POSTCODE_UNRESOLVED", "Destination postcode could not be resolved to coordinates.")
            ]
        };
    }

    private static TravelCoverageDestinationOutcome BuildRouteUnavailable(
        TravelCoverageDestinationRequest destination,
        LocationCoordinates? coordinates)
    {
        return new TravelCoverageDestinationOutcome
        {
            CorrelationId = destination.CorrelationId,
            Postcode = NormalisePostcode(destination.Postcode),
            Status = TravelCoverageStatus.RouteUnavailable,
            Coordinates = coordinates,
            Warnings =
            [
                new TravelCoverageWarning("ROUTE_UNAVAILABLE", "Travel route could not be resolved.")
            ]
        };
    }

    private static string BuildDestinationKey(TravelCoverageDestinationRequest destination)
    {
        if (!string.IsNullOrWhiteSpace(destination.CorrelationId))
            return destination.CorrelationId.Trim();

        return NormalisePostcode(destination.Postcode);
    }

    private static string NormalisePostcode(string? postcode)
        => string.Join(
            " ",
            (postcode ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
