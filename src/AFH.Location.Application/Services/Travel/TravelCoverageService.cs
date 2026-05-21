using AFH.Location.Application.Models.Travel;
using AFH.Location.Application.Abstractions.Travel;

using AFH.Location.Domain.Travel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace AFH.Location.Application.Services.Travel;

public sealed class TravelCoverageService : ITravelCoverageService
{
    private readonly IPostcodeCoordinateResolver _coordinateResolver;
    private readonly ITravelRouteOutcomeProvider _routeOutcomeProvider;
    private readonly ILogger<TravelCoverageService> _logger;
    private readonly IConfiguration? _configuration;

    public TravelCoverageService(
        IPostcodeCoordinateResolver coordinateResolver,
        ITravelRouteOutcomeProvider routeOutcomeProvider,
        ILogger<TravelCoverageService> logger,
        IConfiguration? configuration = null)
    {
        _coordinateResolver = coordinateResolver;
        _routeOutcomeProvider = routeOutcomeProvider;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<TravelCoverageResult> EvaluateAsync(TravelCoverageRequest request, CancellationToken ct)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var sourcePostcode = NormalisePostcode(request.SourcePostcode);
        var source = await _coordinateResolver.ResolveAsync(sourcePostcode, ct);

        if (!source.Succeeded)
        {
            totalStopwatch.Stop();
            LogTotalRequestTiming(totalStopwatch, request.Destinations.Count);

            return PresentResult(new TravelCoverageResult
            {
                SourcePostcode = sourcePostcode,
                TimeContext = request.TimeContext,
                RequestContext = request.RequestContext,
                Destinations = request.Destinations.Select(destination => BuildSourceUnresolved(destination)).ToList()
            });
        }

        var destinationResolutions = await ResolveDestinationsAsync(request.Destinations, ct);
        var routeDestinations = BuildRouteDestinations(sourcePostcode, source.Coordinates!, destinationResolutions);
        var intervals = BuildSlotIntervals(request.TimeContext);

        var destinationSlots = request.Destinations.ToDictionary(
            d => BuildDestinationKey(d),
            d => new List<TravelCoverageSlotOutcome>(),
            StringComparer.OrdinalIgnoreCase);

        var isTimeDependent = request.TimeContext.TimingMode == TravelCoverageTimingMode.DepartureTime;

        if (isTimeDependent && intervals.Count > 1)
        {
            var adaptiveSlots = await EvaluateTimeDependentAdaptiveAsync(
                request,
                sourcePostcode,
                source.Coordinates!,
                destinationResolutions,
                routeDestinations,
                intervals,
                ct);

            foreach (var item in adaptiveSlots)
                destinationSlots[item.Key].AddRange(item.Value);
        }
        else
        {
            await EvaluateReusableRoutesAsync(
                request,
                sourcePostcode,
                source.Coordinates!,
                destinationResolutions,
                routeDestinations,
                intervals,
                destinationSlots,
                ct);
        }

        var outcomes = BuildDestinationOutcomes(request.Destinations, destinationResolutions, destinationSlots);

        _logger.LogInformation(
            "Travel coverage evaluation complete. CorrelationId={CorrelationId} DestinationCount={DestinationCount} RouteDestinationCount={RouteDestinationCount}",
            request.RequestContext.CorrelationId,
            outcomes.Count,
            routeDestinations.Count);
        totalStopwatch.Stop();
        LogTotalRequestTiming(totalStopwatch, outcomes.Count);

        return PresentResult(new TravelCoverageResult
        {
            SourcePostcode = sourcePostcode,
            SourceCoordinates = source.Coordinates,
            TimeContext = request.TimeContext,
            Destinations = outcomes,
            RequestContext = request.RequestContext
        });
    }

    private static TravelCoverageResult PresentResult(TravelCoverageResult result)
    {
        return result with
        {
            Destinations = result.Destinations
                .Select(destination => destination with
                {
                    PresentedSlots = TravelCoverageResponsePresenter.PresentSlots(
                        result.TimeContext.SlotResponseMode,
                        result.TimeContext,
                        destination)
                })
                .ToList()
        };
    }

    private Dictionary<string, LocationCoordinates> BuildRouteDestinations(
        string sourcePostcode,
        LocationCoordinates sourceCoordinates,
        IReadOnlyDictionary<string, PostcodeCoordinateResolution> destinationResolutions)
    {
        var routeDestinations = new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in destinationResolutions)
        {
            if (!item.Value.Succeeded)
                continue;

            var destPostcode = NormalisePostcode(item.Value.Postcode);
            var destCoords = item.Value.Coordinates!;

            if (!IsSameOrigin(sourcePostcode, sourceCoordinates, destPostcode, destCoords))
                routeDestinations[item.Key] = destCoords;
        }

        return routeDestinations;
    }

    private List<TravelCoverageSlotInterval> BuildSlotIntervals(TravelCoverageTimeContext timeContext)
    {
        var intervals = new List<TravelCoverageSlotInterval>();
        if (!timeContext.StartTime.HasValue || !timeContext.EndTime.HasValue)
        {
            intervals.Add(new TravelCoverageSlotInterval(timeContext.RequestedDepartureTime ?? timeContext.StartTime, timeContext.EndTime));
            return intervals;
        }

        var start = timeContext.StartTime.Value;
        var end = timeContext.EndTime.Value;
        var interval = timeContext.SearchIntervalMinutes ?? 30;
        if (interval <= 0)
            interval = 30;

        var current = start;
        while (current < end)
        {
            var next = current.AddMinutes(interval);
            if (next > end)
                next = end;

            intervals.Add(new TravelCoverageSlotInterval(current, next));
            current = next;
        }

        var maxSlots = GetConfiguredPositiveInt("TravelCoverage:MaxGeneratedSlots", 24);
        if (intervals.Count > maxSlots)
        {
            _logger.LogWarning(
                "Generated {GeneratedCount} intervals, which exceeds the max configured slots of {MaxSlots}. Capping to {MaxSlots}.",
                intervals.Count,
                maxSlots,
                maxSlots);

            intervals = intervals.Take(maxSlots).ToList();
        }

        return intervals;
    }

    private async Task EvaluateReusableRoutesAsync(
        TravelCoverageRequest request,
        string sourcePostcode,
        LocationCoordinates sourceCoordinates,
        IReadOnlyDictionary<string, PostcodeCoordinateResolution> destinationResolutions,
        IReadOnlyDictionary<string, LocationCoordinates> routeDestinations,
        IReadOnlyList<TravelCoverageSlotInterval> intervals,
        IDictionary<string, List<TravelCoverageSlotOutcome>> destinationSlots,
        CancellationToken ct)
    {
        var intervalRoutes = BuildSameOriginRoutes(sourcePostcode, sourceCoordinates, destinationResolutions);
        var activeDestinations = routeDestinations.Keys
            .Where(key => !intervalRoutes.ContainsKey(key))
            .ToList();

        if (activeDestinations.Count > 0)
        {
            var batchDestinations = activeDestinations.ToDictionary(
                key => key,
                key => routeDestinations[key],
                StringComparer.OrdinalIgnoreCase);

            var providerRoutes = await _routeOutcomeProvider.GetOutcomesAsync(
                new TravelRouteOutcomeRequest
                {
                    Source = sourceCoordinates,
                    Destinations = batchDestinations,
                    TimeContext = request.TimeContext
                },
                ct);

            foreach (var route in providerRoutes)
                intervalRoutes[route.Key] = route.Value;
        }

        foreach (var interval in intervals)
        {
            foreach (var destination in request.Destinations)
            {
                var key = BuildDestinationKey(destination);
                if (!destinationResolutions.TryGetValue(key, out var resolved) || !resolved.Succeeded)
                    continue;

                destinationSlots[key].Add(BuildSlotOutcome(
                    interval,
                    destination,
                    intervalRoutes.TryGetValue(key, out var route) ? route : null));
            }
        }
    }

    private static Dictionary<string, TravelRouteOutcome> BuildSameOriginRoutes(
        string sourcePostcode,
        LocationCoordinates sourceCoordinates,
        IReadOnlyDictionary<string, PostcodeCoordinateResolution> destinationResolutions)
    {
        var intervalRoutes = new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in destinationResolutions)
        {
            if (!item.Value.Succeeded)
                continue;

            var destPostcode = NormalisePostcode(item.Value.Postcode);
            var destCoords = item.Value.Coordinates!;

            if (IsSameOrigin(sourcePostcode, sourceCoordinates, destPostcode, destCoords))
                intervalRoutes[item.Key] = new TravelRouteOutcome(0, 0d, "High", TravelRouteResolutionSource.Unknown);
        }

        return intervalRoutes;
    }

    private static List<TravelCoverageDestinationOutcome> BuildDestinationOutcomes(
        IReadOnlyList<TravelCoverageDestinationRequest> destinations,
        IReadOnlyDictionary<string, PostcodeCoordinateResolution> destinationResolutions,
        IReadOnlyDictionary<string, List<TravelCoverageSlotOutcome>> destinationSlots)
    {
        var outcomes = new List<TravelCoverageDestinationOutcome>(destinations.Count);

        foreach (var destination in destinations)
        {
            var key = BuildDestinationKey(destination);
            if (!destinationResolutions.TryGetValue(key, out var resolved) || !resolved.Succeeded)
            {
                outcomes.Add(BuildDestinationUnresolved(destination));
                continue;
            }

            var slots = destinationSlots[key];
            var status = slots.Any(s => s.Route is not null)
                ? TravelCoverageStatus.Succeeded
                : TravelCoverageStatus.RouteUnavailable;
            var warnings = status == TravelCoverageStatus.RouteUnavailable
                ? [new TravelCoverageWarning("ROUTE_UNAVAILABLE", "Travel route could not be resolved.")]
                : new List<TravelCoverageWarning>();
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

        return outcomes;
    }

    private void LogTotalRequestTiming(Stopwatch totalStopwatch, int destinationCount)
    {
        _logger.LogInformation(
            "Location travel coverage phase timing. Phase={Phase} DurationMs={DurationMs} DestinationCount={DestinationCount}",
            "TotalRequest",
            totalStopwatch.ElapsedMilliseconds,
            destinationCount);
    }

    private static bool IsSameOrigin(
        string sourcePostcode,
        LocationCoordinates sourceCoordinates,
        string destinationPostcode,
        LocationCoordinates destinationCoordinates)
    {
        return string.Equals(sourcePostcode, destinationPostcode, StringComparison.OrdinalIgnoreCase)
               || (sourceCoordinates.Latitude == destinationCoordinates.Latitude &&
                   sourceCoordinates.Longitude == destinationCoordinates.Longitude);
    }

    private async Task<Dictionary<string, PostcodeCoordinateResolution>> ResolveDestinationsAsync(
        IReadOnlyList<TravelCoverageDestinationRequest> destinations,
        CancellationToken ct)
    {
        var distinctDestinations = destinations
            .GroupBy(destination => BuildDestinationKey(destination), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Postcode,
                StringComparer.OrdinalIgnoreCase);

        var resolved = await _coordinateResolver.ResolveManyAsync(distinctDestinations, ct);

        return resolved.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, List<TravelCoverageSlotOutcome>>> EvaluateTimeDependentAdaptiveAsync(
        TravelCoverageRequest request,
        string sourcePostcode,
        LocationCoordinates sourceCoordinates,
        IReadOnlyDictionary<string, PostcodeCoordinateResolution> destinationResolutions,
        IReadOnlyDictionary<string, LocationCoordinates> routeDestinations,
        IReadOnlyList<TravelCoverageSlotInterval> intervals,
        CancellationToken ct)
    {
        var options = GetAdaptiveSlotOptions();
        var destinationByKey = request.Destinations
            .GroupBy(BuildDestinationKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var eligibleKeys = destinationByKey.Keys
            .Where(key => destinationResolutions.TryGetValue(key, out var resolved) && resolved.Succeeded)
            .ToList();

        var evaluated = eligibleKeys.ToDictionary(
            key => key,
            _ => new SortedDictionary<int, TravelCoverageSlotOutcome>(),
            StringComparer.OrdinalIgnoreCase);

        var evaluatedSlotIndices = new HashSet<int>();
        var anchorIndices = BuildAnchorIndices(intervals.Count);
        foreach (var index in anchorIndices)
        {
            var outcomes = await EvaluateTimeDependentIntervalAsync(
                request,
                sourcePostcode,
                sourceCoordinates,
                destinationResolutions,
                routeDestinations,
                intervals[index],
                eligibleKeys,
                ct);

            StoreEvaluatedOutcomes(index, outcomes, evaluated);
            evaluatedSlotIndices.Add(index);
        }

        var expandedSlotIndices = new HashSet<int>();
        foreach (var segment in BuildAnchorSegments(anchorIndices))
        {
            await ExpandSegmentAsync(
                segment.Left,
                segment.Right,
                depth: 0,
                eligibleKeys,
                request,
                sourcePostcode,
                sourceCoordinates,
                destinationResolutions,
                routeDestinations,
                intervals,
                destinationByKey,
                evaluated,
                evaluatedSlotIndices,
                expandedSlotIndices,
                options,
                ct);
        }

        var logicalSlots = intervals.Count;
        var evaluatedSlots = evaluatedSlotIndices.Count;
        var azureCallsAvoided = Math.Max(0, logicalSlots - evaluatedSlots);
        _logger.LogInformation(
            "Location travel coverage adaptive slot diagnostics. LogicalSlots={LogicalSlots} EvaluatedSlots={EvaluatedSlots} AnchorSlots={AnchorSlots} ExpandedSlots={ExpandedSlots} AzureCallsAvoided={AzureCallsAvoided}",
            logicalSlots,
            evaluatedSlots,
            anchorIndices.Count,
            expandedSlotIndices.Count,
            azureCallsAvoided);

        var slots = request.Destinations.ToDictionary(
            BuildDestinationKey,
            _ => new List<TravelCoverageSlotOutcome>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var destination in request.Destinations)
        {
            var key = BuildDestinationKey(destination);
            if (!destinationResolutions.TryGetValue(key, out var resolved) || !resolved.Succeeded)
                continue;

            for (var index = 0; index < intervals.Count; index++)
            {
                if (evaluated.TryGetValue(key, out var destinationEvaluations) &&
                    destinationEvaluations.TryGetValue(index, out var exact))
                {
                    slots[key].Add(exact with
                    {
                        StartTime = intervals[index].Start,
                        EndTime = intervals[index].End
                    });
                    continue;
                }

                slots[key].Add(BuildEstimatedSlotOutcome(
                    intervals[index],
                    index,
                    destination,
                    evaluated.TryGetValue(key, out var known)
                        ? known
                        : new SortedDictionary<int, TravelCoverageSlotOutcome>()));
            }
        }

        return slots;
    }

    private async Task ExpandSegmentAsync(
        int left,
        int right,
        int depth,
        IReadOnlyCollection<string> candidateKeys,
        TravelCoverageRequest request,
        string sourcePostcode,
        LocationCoordinates sourceCoordinates,
        IReadOnlyDictionary<string, PostcodeCoordinateResolution> destinationResolutions,
        IReadOnlyDictionary<string, LocationCoordinates> routeDestinations,
        IReadOnlyList<TravelCoverageSlotInterval> intervals,
        IReadOnlyDictionary<string, TravelCoverageDestinationRequest> destinationByKey,
        IDictionary<string, SortedDictionary<int, TravelCoverageSlotOutcome>> evaluated,
        ISet<int> evaluatedSlotIndices,
        ISet<int> expandedSlotIndices,
        AdaptiveSlotEvaluationOptions options,
        CancellationToken ct)
    {
        if (right - left <= 1 || depth >= options.MaxExpansionDepth || evaluatedSlotIndices.Count >= options.MaxEvaluatedSlots)
            return;

        var keysNeedingExpansion = candidateKeys
            .Where(key => evaluated.TryGetValue(key, out var destinationEvaluations)
                          && destinationEvaluations.TryGetValue(left, out var leftOutcome)
                          && destinationEvaluations.TryGetValue(right, out var rightOutcome)
                          && destinationByKey.TryGetValue(key, out var destination)
                          && ShouldExpandSegment(leftOutcome, rightOutcome, destination, options))
            .ToList();

        if (keysNeedingExpansion.Count == 0)
            return;

        var midpoint = left + ((right - left) / 2);
        if (midpoint == left || midpoint == right)
            return;

        var keysMissingMidpoint = keysNeedingExpansion
            .Where(key => !evaluated.TryGetValue(key, out var destinationEvaluations) ||
                          !destinationEvaluations.ContainsKey(midpoint))
            .ToList();

        if (keysMissingMidpoint.Count > 0 && evaluatedSlotIndices.Count < options.MaxEvaluatedSlots)
        {
            var outcomes = await EvaluateTimeDependentIntervalAsync(
                request,
                sourcePostcode,
                sourceCoordinates,
                destinationResolutions,
                routeDestinations,
                intervals[midpoint],
                keysMissingMidpoint,
                ct);

            StoreEvaluatedOutcomes(midpoint, outcomes, evaluated);
            evaluatedSlotIndices.Add(midpoint);
            expandedSlotIndices.Add(midpoint);
        }

        await ExpandSegmentAsync(
            left,
            midpoint,
            depth + 1,
            keysNeedingExpansion,
            request,
            sourcePostcode,
            sourceCoordinates,
            destinationResolutions,
            routeDestinations,
            intervals,
            destinationByKey,
            evaluated,
            evaluatedSlotIndices,
            expandedSlotIndices,
            options,
            ct);

        await ExpandSegmentAsync(
            midpoint,
            right,
            depth + 1,
            keysNeedingExpansion,
            request,
            sourcePostcode,
            sourceCoordinates,
            destinationResolutions,
            routeDestinations,
            intervals,
            destinationByKey,
            evaluated,
            evaluatedSlotIndices,
            expandedSlotIndices,
            options,
            ct);
    }

    private async Task<Dictionary<string, TravelCoverageSlotOutcome>> EvaluateTimeDependentIntervalAsync(
        TravelCoverageRequest request,
        string sourcePostcode,
        LocationCoordinates sourceCoordinates,
        IReadOnlyDictionary<string, PostcodeCoordinateResolution> destinationResolutions,
        IReadOnlyDictionary<string, LocationCoordinates> routeDestinations,
        TravelCoverageSlotInterval interval,
        IReadOnlyCollection<string> destinationKeys,
        CancellationToken ct)
    {
        var requestedKeys = destinationKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var intervalTimeContext = request.TimeContext with { RequestedDepartureTime = interval.Start };
        var intervalRoutes = new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in destinationResolutions)
        {
            if (!requestedKeys.Contains(item.Key) || !item.Value.Succeeded)
                continue;

            var destPostcode = NormalisePostcode(item.Value.Postcode);
            var destCoords = item.Value.Coordinates!;

            var isSameOrigin = string.Equals(sourcePostcode, destPostcode, StringComparison.OrdinalIgnoreCase)
                || (sourceCoordinates.Latitude == destCoords.Latitude && sourceCoordinates.Longitude == destCoords.Longitude);

            if (isSameOrigin)
                intervalRoutes[item.Key] = new TravelRouteOutcome(0, 0d, "High", TravelRouteResolutionSource.Unknown);
        }

        var activeDestinations = routeDestinations.Keys
            .Where(key => requestedKeys.Contains(key) && !intervalRoutes.ContainsKey(key))
            .ToList();
        if (activeDestinations.Count > 0)
        {
            var batchDestinations = activeDestinations.ToDictionary(key => key, key => routeDestinations[key], StringComparer.OrdinalIgnoreCase);
            var providerRoutes = await _routeOutcomeProvider.GetOutcomesAsync(
                new TravelRouteOutcomeRequest
                {
                    Source = sourceCoordinates,
                    Destinations = batchDestinations,
                    TimeContext = intervalTimeContext
                },
                ct);

            foreach (var route in providerRoutes)
                intervalRoutes[route.Key] = route.Value;
        }

        var outcomes = new Dictionary<string, TravelCoverageSlotOutcome>(StringComparer.OrdinalIgnoreCase);
        foreach (var destination in request.Destinations)
        {
            var key = BuildDestinationKey(destination);
            if (!requestedKeys.Contains(key) ||
                !destinationResolutions.TryGetValue(key, out var resolved) ||
                !resolved.Succeeded)
            {
                continue;
            }

            outcomes[key] = BuildSlotOutcome(interval, destination, intervalRoutes.TryGetValue(key, out var route) ? route : null);
        }

        return outcomes;
    }

    private AdaptiveSlotEvaluationOptions GetAdaptiveSlotOptions()
        => new(
            GetConfiguredPositiveInt("TravelCoverage:AdaptiveAnchors:TravelTimeToleranceMinutes", 5),
            GetConfiguredPositiveDouble("TravelCoverage:AdaptiveAnchors:DistanceToleranceMiles", 2d),
            GetConfiguredPositiveDouble("TravelCoverage:AdaptiveAnchors:CoverageThresholdBuffer", 5d),
            GetConfiguredPositiveInt("TravelCoverage:AdaptiveAnchors:MaxExpansionDepth", 2),
            GetConfiguredPositiveInt("TravelCoverage:AdaptiveAnchors:MaxEvaluatedSlots", 8));

    private int GetConfiguredPositiveInt(string key, int defaultValue)
    {
        if (_configuration == null)
            return defaultValue;

        var value = _configuration.GetSection(key)?.Value;
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
    }

    private double GetConfiguredPositiveDouble(string key, double defaultValue)
    {
        if (_configuration == null)
            return defaultValue;

        var value = _configuration.GetSection(key)?.Value;
        return double.TryParse(value, out var parsed) && parsed > 0d ? parsed : defaultValue;
    }

    private static List<int> BuildAnchorIndices(int slotCount)
    {
        var indices = new[] { 0, slotCount / 2, slotCount - 1 };
        return indices
            .Where(index => index >= 0 && index < slotCount)
            .Distinct()
            .Order()
            .ToList();
    }

    private static IEnumerable<(int Left, int Right)> BuildAnchorSegments(IReadOnlyList<int> anchorIndices)
    {
        for (var i = 0; i < anchorIndices.Count - 1; i++)
            yield return (anchorIndices[i], anchorIndices[i + 1]);
    }

    private static void StoreEvaluatedOutcomes(
        int index,
        IReadOnlyDictionary<string, TravelCoverageSlotOutcome> outcomes,
        IDictionary<string, SortedDictionary<int, TravelCoverageSlotOutcome>> evaluated)
    {
        foreach (var item in outcomes)
        {
            if (!evaluated.TryGetValue(item.Key, out var destinationEvaluations))
            {
                destinationEvaluations = new SortedDictionary<int, TravelCoverageSlotOutcome>();
                evaluated[item.Key] = destinationEvaluations;
            }

            destinationEvaluations[index] = item.Value;
        }
    }

    private static bool ShouldExpandSegment(
        TravelCoverageSlotOutcome left,
        TravelCoverageSlotOutcome right,
        TravelCoverageDestinationRequest destination,
        AdaptiveSlotEvaluationOptions options)
    {
        if (left.Route is null || right.Route is null || !left.Route.HasUsableRoute || !right.Route.HasUsableRoute)
            return true;

        var travelTimeVariance = Math.Abs((left.Route.TravelTimeMinutes ?? 0) - (right.Route.TravelTimeMinutes ?? 0));
        if (travelTimeVariance > options.TravelTimeToleranceMinutes)
            return true;

        var distanceVariance = Math.Abs((left.Route.DistanceMiles ?? 0d) - (right.Route.DistanceMiles ?? 0d));
        if (distanceVariance > options.DistanceToleranceMiles)
            return true;

        if (left.Coverage?.IsWithinCoverage != right.Coverage?.IsWithinCoverage)
            return true;

        return IsNearCoverageThreshold(left.Route, destination, options.CoverageThresholdBuffer)
               || IsNearCoverageThreshold(right.Route, destination, options.CoverageThresholdBuffer);
    }

    private static bool IsNearCoverageThreshold(
        TravelRouteOutcome route,
        TravelCoverageDestinationRequest destination,
        double thresholdBuffer)
    {
        if (destination.MaxTravelTimeMinutes.HasValue &&
            route.TravelTimeMinutes.HasValue &&
            Math.Abs(route.TravelTimeMinutes.Value - destination.MaxTravelTimeMinutes.Value) <= thresholdBuffer)
        {
            return true;
        }

        return destination.MaxDistanceMiles.HasValue &&
               route.DistanceMiles.HasValue &&
               Math.Abs(route.DistanceMiles.Value - destination.MaxDistanceMiles.Value) <= thresholdBuffer;
    }

    private static TravelCoverageSlotOutcome BuildEstimatedSlotOutcome(
        TravelCoverageSlotInterval interval,
        int index,
        TravelCoverageDestinationRequest destination,
        SortedDictionary<int, TravelCoverageSlotOutcome> evaluated)
    {
        if (evaluated.Count == 0)
            return BuildSlotOutcome(interval, destination, null);

        var left = evaluated.LastOrDefault(item => item.Key <= index);
        var right = evaluated.FirstOrDefault(item => item.Key >= index);
        var candidates = new[] { left.Value, right.Value }
            .Where(outcome => outcome is not null)
            .Cast<TravelCoverageSlotOutcome>()
            .ToList();

        if (candidates.Count == 0)
            return BuildSlotOutcome(interval, destination, null);

        var representative = candidates
            .OrderByDescending(outcome => outcome.Route?.TravelTimeMinutes ?? 0)
            .ThenByDescending(outcome => outcome.Route?.DistanceMiles ?? 0d)
            .First();

        var route = representative.Route;
        if (route is null || !route.HasUsableRoute)
            return BuildSlotOutcome(interval, destination, null);

        return BuildSlotOutcome(interval, destination, route);
    }

    private static TravelCoverageSlotOutcome BuildSlotOutcome(
        TravelCoverageSlotInterval interval,
        TravelCoverageDestinationRequest destination,
        TravelRouteOutcome? route)
    {
        if (route is null || !route.HasUsableRoute)
        {
            return new TravelCoverageSlotOutcome
            {
                StartTime = interval.Start,
                EndTime = interval.End,
                Route = null,
                Coverage = null
            };
        }

        var policy = new TravelCoveragePolicy(destination.MaxTravelTimeMinutes, destination.MaxDistanceMiles);
        var decision = policy.Evaluate(route);

        return new TravelCoverageSlotOutcome
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
        };
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

    private sealed record TravelCoverageSlotInterval(DateTimeOffset? Start, DateTimeOffset? End);

    private sealed record AdaptiveSlotEvaluationOptions(
        int TravelTimeToleranceMinutes,
        double DistanceToleranceMiles,
        double CoverageThresholdBuffer,
        int MaxExpansionDepth,
        int MaxEvaluatedSlots);
}
