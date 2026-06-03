using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Application.Models.Travel;
using AFH.Location.Application.Services.Travel;
using AFH.Location.Domain.Travel;

namespace AFH.Location.Infrastructure.External.Maps;

public sealed class TravelRouteOutcomeProvider : ITravelRouteOutcomeProvider
{
    private readonly IRouteMatrixService _routeMatrixService;
    private readonly IRouteMatrixPolicyProvider _policyProvider;

    public TravelRouteOutcomeProvider(
        IRouteMatrixService routeMatrixService,
        IRouteMatrixPolicyProvider policyProvider)
    {
        _routeMatrixService = routeMatrixService;
        _policyProvider = policyProvider;
    }

    public async Task<IReadOnlyDictionary<string, TravelRouteOutcome>> GetOutcomesAsync(
        TravelRouteOutcomeRequest request,
        CancellationToken ct)
    {
        if (request.Destinations.Count == 0)
            return new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase);

        var policy = await _policyProvider.GetAsync(ct);
        var maxDestinations = Math.Max(1, policy.MaxDestinationsPerCall);
        var results = new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in Batch(request.Destinations, maxDestinations))
        {
            var departAt = request.TimeContext?.TimingMode == TravelCoverageTimingMode.DepartureTime
                ? request.TimeContext.RequestedDepartureTime
                : null;

            var routes = await _routeMatrixService.GetOneToManyAsync(
                (request.Source.Latitude, request.Source.Longitude),
                batch.ToDictionary(
                    item => item.Key,
                    item => (item.Value.Latitude, item.Value.Longitude),
                    StringComparer.OrdinalIgnoreCase),
                departAt: departAt,
                ct: ct);

            foreach (var route in routes)
            {
                if (IsSyntheticFallback(route.Value) &&
                    batch.TryGetValue(route.Key, out var destination) &&
                    ProximateRouteFallback.TryEstimate(request.Source, destination, out var fallback))
                {
                    results[route.Key] = new TravelRouteOutcome(
                        fallback.TravelTimeMinutes,
                        fallback.DistanceMiles,
                        fallback.Confidence,
                        TravelRouteResolutionSource.Unknown);
                    continue;
                }

                // Use >= 0 so that a genuine provider result of 0 min / 0 miles
                // (e.g. same-coordinate or sub-60s route) is preserved as usable.
                // Only a negative value (sentinel for unavailable) becomes null.
                results[route.Key] = new TravelRouteOutcome(
                    route.Value.EtaMinutes >= 0 ? route.Value.EtaMinutes : null,
                    route.Value.DistanceMiles >= 0 ? route.Value.DistanceMiles : null,
                    route.Value.Confidence,
                    route.Value.ResolutionSource);
            }
        }

        return results;
    }

    private static bool IsSyntheticFallback(RouteResult result)
        => result.EtaMinutes == 0
           && result.DistanceMiles == 0d
           && string.Equals(result.Confidence, "Low", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<IReadOnlyDictionary<string, LocationCoordinates>> Batch(
        IReadOnlyDictionary<string, LocationCoordinates> input,
        int batchSize)
    {
        var chunk = new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in input)
        {
            chunk[kv.Key] = kv.Value;
            if (chunk.Count >= batchSize)
            {
                yield return new Dictionary<string, LocationCoordinates>(chunk, StringComparer.OrdinalIgnoreCase);
                chunk.Clear();
            }
        }

        if (chunk.Count > 0)
            yield return new Dictionary<string, LocationCoordinates>(chunk, StringComparer.OrdinalIgnoreCase);
    }
}
