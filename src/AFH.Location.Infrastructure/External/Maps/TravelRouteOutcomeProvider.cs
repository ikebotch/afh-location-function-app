using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Application.Models.V1.Travel;
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
            var routes = await _routeMatrixService.GetOneToManyAsync(
                (request.Source.Latitude, request.Source.Longitude),
                batch.ToDictionary(
                    item => item.Key,
                    item => (item.Value.Latitude, item.Value.Longitude),
                    StringComparer.OrdinalIgnoreCase),
                ct);

            foreach (var route in routes)
            {
                results[route.Key] = new TravelRouteOutcome(
                    route.Value.EtaMinutes > 0 ? route.Value.EtaMinutes : null,
                    route.Value.DistanceMiles > 0 ? route.Value.DistanceMiles : null,
                    route.Value.Confidence,
                    route.Value.ResolutionSource);
            }
        }

        return results;
    }

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
