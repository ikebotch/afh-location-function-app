using AFH.Location.Application.Models.Travel;
using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;

namespace AFH.Location.Application.Services.Travel;

public sealed class RouteMatrixCoordinator
{
    private readonly IRouteMatrixService _matrix;
    private readonly IRouteMatrixPolicyProvider _policyProvider;

    public RouteMatrixCoordinator(IRouteMatrixService matrix, IRouteMatrixPolicyProvider policyProvider)
    {
        _matrix = matrix;
        _policyProvider = policyProvider;
    }

    public async Task<IReadOnlyDictionary<string, RouteResult>> GetRoutesAsync(
        IReadOnlyDictionary<string, (double Lat, double Lng)> origins,
        (double Lat, double Lng) destination,
        CancellationToken ct)
    {
        var policy = await _policyProvider.GetAsync(ct);
        var max = Math.Max(1, policy.MaxOriginsPerCall);

        var result = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in Batch(origins, max))
        {
            var partial = await _matrix.GetAdviserToDestinationAsync(batch, destination, ct);
            foreach (var kv in partial)
                result[kv.Key] = kv.Value;
        }

        return result;
    }

    public Task<IReadOnlyDictionary<string, RouteResult>> GetRoutesFromOriginAsync(
        (double Lat, double Lng) origin,
        IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
        CancellationToken ct)
    {
        if (destinations.Count == 0)
        {
            return Task.FromResult<IReadOnlyDictionary<string, RouteResult>>(
                new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase));
        }

        return _matrix.GetOneToManyAsync(origin, destinations, departAt: null, ct: ct);
    }

    private static IEnumerable<IReadOnlyDictionary<string, (double Lat, double Lng)>> Batch(
        IReadOnlyDictionary<string, (double Lat, double Lng)> input,
        int batchSize)
    {
        var chunk = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in input)
        {
            chunk[kv.Key] = kv.Value;
            if (chunk.Count >= batchSize)
            {
                yield return new Dictionary<string, (double Lat, double Lng)>(chunk, StringComparer.OrdinalIgnoreCase);
                chunk.Clear();
            }
        }
        if (chunk.Count > 0)
            yield return new Dictionary<string, (double Lat, double Lng)>(chunk, StringComparer.OrdinalIgnoreCase);
    }
}
