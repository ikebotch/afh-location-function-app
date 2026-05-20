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

        foreach (var item in destinationResolutions)
        {
            if (!item.Value.Succeeded)
                continue;

            var destPostcode = NormalisePostcode(item.Value.Postcode);
            var destCoords = item.Value.Coordinates!;

            bool isSameOrigin = string.Equals(sourcePostcode, destPostcode, StringComparison.OrdinalIgnoreCase)
                || (source.Coordinates != null && source.Coordinates.Latitude == destCoords.Latitude && source.Coordinates.Longitude == destCoords.Longitude);

            if (!isSameOrigin)
            {
                routeDestinations[item.Key] = destCoords;
            }
        }

        // 1. Generate sequence of time intervals
        var intervals = new List<(DateTimeOffset? Start, DateTimeOffset? End)>();
        if (!request.TimeContext.StartTime.HasValue || !request.TimeContext.EndTime.HasValue)
        {
            intervals.Add((request.TimeContext.RequestedDepartureTime ?? request.TimeContext.StartTime, request.TimeContext.EndTime));
        }
        else
        {
            var start = request.TimeContext.StartTime.Value;
            var end = request.TimeContext.EndTime.Value;
            var interval = request.TimeContext.SearchIntervalMinutes ?? 30;
            if (interval <= 0)
            {
                interval = 30;
            }

            var current = start;
            while (current < end)
            {
                var next = current.AddMinutes(interval);
                if (next > end)
                {
                    next = end;
                }
                intervals.Add((current, next));
                current = next;
            }
        }

        var destinationSlots = request.Destinations.ToDictionary(
            d => BuildDestinationKey(d),
            d => new List<TravelCoverageSlotOutcome>(),
            StringComparer.OrdinalIgnoreCase);

        var isTimeDependent = request.TimeContext.TimingMode == TravelCoverageTimingMode.DepartureTime;

        if (isTimeDependent && intervals.Count > 1)
        {
            // Evaluate routing and coverage per interval departure time
            foreach (var interval in intervals)
            {
                var intervalTimeContext = request.TimeContext with { RequestedDepartureTime = interval.Start };
                var intervalRoutes = new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase);

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
                        intervalRoutes[item.Key] = new TravelRouteOutcome(0, 0d, "High", TravelRouteResolutionSource.Unknown);
                    }
                }

                var activeDestinations = routeDestinations.Keys.Where(k => !intervalRoutes.ContainsKey(k)).ToList();
                if (activeDestinations.Count > 0)
                {
                    var batchDestinations = activeDestinations.ToDictionary(k => k, k => routeDestinations[k], StringComparer.OrdinalIgnoreCase);
                    var providerRoutes = await _routeOutcomeProvider.GetOutcomesAsync(
                        new TravelRouteOutcomeRequest
                        {
                            Source = source.Coordinates!,
                            Destinations = batchDestinations,
                            TimeContext = intervalTimeContext
                        },
                        ct);

                    foreach (var route in providerRoutes)
                    {
                        intervalRoutes[route.Key] = route.Value;
                    }
                }

                foreach (var destination in request.Destinations)
                {
                    var key = BuildDestinationKey(destination);
                    if (!destinationResolutions.TryGetValue(key, out var resolved) || !resolved.Succeeded)
                    {
                        continue;
                    }

                    if (!intervalRoutes.TryGetValue(key, out var route) || !route.HasUsableRoute)
                    {
                        destinationSlots[key].Add(new TravelCoverageSlotOutcome
                        {
                            StartTime = interval.Start,
                            EndTime = interval.End,
                            Route = null,
                            Coverage = null
                        });
                        continue;
                    }

                    var policy = new TravelCoveragePolicy(destination.MaxTravelTimeMinutes, destination.MaxDistanceMiles);
                    var decision = policy.Evaluate(route);

                    destinationSlots[key].Add(new TravelCoverageSlotOutcome
                    {
                        StartTime = interval.Start,
                        EndTime = interval.End,
                        Route = route,
                        Coverage = new TravelCoverageOutcome
                        {
                            IsWithinCoverage = decision.IsWithinCoverage,
                            MaxTravelTimeMinutes = destination.MaxTravelTimeMinutes,
                            MaxDistanceMiles = destination.MaxDistanceMiles
                        }
                    });
                }
            }
        }
        else
        {
            // Evaluate once (TimeIndependent or single interval) and apply across all intervals
            var intervalRoutes = new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase);

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
                    intervalRoutes[item.Key] = new TravelRouteOutcome(0, 0d, "High", TravelRouteResolutionSource.Unknown);
                }
            }

            var activeDestinations = routeDestinations.Keys.Where(k => !intervalRoutes.ContainsKey(k)).ToList();
            if (activeDestinations.Count > 0)
            {
                var batchDestinations = activeDestinations.ToDictionary(k => k, k => routeDestinations[k], StringComparer.OrdinalIgnoreCase);
                var providerRoutes = await _routeOutcomeProvider.GetOutcomesAsync(
                    new TravelRouteOutcomeRequest
                    {
                        Source = source.Coordinates!,
                        Destinations = batchDestinations,
                        TimeContext = request.TimeContext
                    },
                    ct);

                foreach (var route in providerRoutes)
                {
                    intervalRoutes[route.Key] = route.Value;
                }
            }

            foreach (var interval in intervals)
            {
                foreach (var destination in request.Destinations)
                {
                    var key = BuildDestinationKey(destination);
                    if (!destinationResolutions.TryGetValue(key, out var resolved) || !resolved.Succeeded)
                    {
                        continue;
                    }

                    if (!intervalRoutes.TryGetValue(key, out var route) || !route.HasUsableRoute)
                    {
                        destinationSlots[key].Add(new TravelCoverageSlotOutcome
                        {
                            StartTime = interval.Start,
                            EndTime = interval.End,
                            Route = null,
                            Coverage = null
                        });
                        continue;
                    }

                    var policy = new TravelCoveragePolicy(destination.MaxTravelTimeMinutes, destination.MaxDistanceMiles);
                    var decision = policy.Evaluate(route);

                    destinationSlots[key].Add(new TravelCoverageSlotOutcome
                    {
                        StartTime = interval.Start,
                        EndTime = interval.End,
                        Route = route,
                        Coverage = new TravelCoverageOutcome
                        {
                            IsWithinCoverage = decision.IsWithinCoverage,
                            MaxTravelTimeMinutes = destination.MaxTravelTimeMinutes,
                            MaxDistanceMiles = destination.MaxDistanceMiles
                        }
                    });
                }
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

            var slots = destinationSlots[key];
            var hasAnyUsableRoute = slots.Any(s => s.Route is not null);
            
            var status = hasAnyUsableRoute ? TravelCoverageStatus.Succeeded : TravelCoverageStatus.RouteUnavailable;
            var warnings = new List<TravelCoverageWarning>();
            if (status == TravelCoverageStatus.RouteUnavailable)
            {
                warnings.Add(new TravelCoverageWarning("ROUTE_UNAVAILABLE", "Travel route could not be resolved."));
            }

            var firstSlot = slots.FirstOrDefault();

            outcomes.Add(new TravelCoverageDestinationOutcome
            {
                CorrelationId = destination.CorrelationId,
                Postcode = NormalisePostcode(destination.Postcode),
                Status = status,
                Coordinates = resolved.Coordinates,
                Route = firstSlot?.Route,
                Coverage = firstSlot?.Coverage,
                Slots = slots,
                Warnings = warnings
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
