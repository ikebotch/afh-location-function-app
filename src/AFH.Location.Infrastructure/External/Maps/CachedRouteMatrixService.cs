using AFH.Location.Application.Abstractions;

namespace AFH.Location.Infrastructure.External.Maps;

public sealed class CachedRouteMatrixService : IRouteMatrixService
{
    private readonly IRouteMatrixService _inner;
    private readonly IRouteCache _cache;

    public CachedRouteMatrixService(IRouteMatrixService inner, IRouteCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
        IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
        (double Lat, double Lng) destination,
        CancellationToken ct)
    {
        var cached = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);
        var misses = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);

        foreach (var adviser in adviserOrigins)
        {
            var key = BuildKey(adviser.Value, destination, adviser.Key);
            if (_cache.TryGet(key, out var route))
                cached[adviser.Key] = route;
            else
                misses[adviser.Key] = adviser.Value;
        }

        if (misses.Count > 0)
        {
            var live = await _inner.GetAdviserToDestinationAsync(misses, destination, ct);
            foreach (var item in live)
            {
                cached[item.Key] = item.Value;
                _cache.Set(BuildKey(misses[item.Key], destination, item.Key), item.Value, item.Value.EtaMinutes > 0 ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(5));
            }
        }

        return cached;
    }

    public async Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
        (double Lat, double Lng) origin,
        IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
        CancellationToken ct)
    {
        var cached = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);
        var misses = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);

        foreach (var destination in destinations)
        {
            var key = BuildKey(origin, destination.Value, destination.Key);
            if (_cache.TryGet(key, out var route))
                cached[destination.Key] = route;
            else
                misses[destination.Key] = destination.Value;
        }

        if (misses.Count > 0)
        {
            var live = await _inner.GetOneToManyAsync(origin, misses, ct);
            foreach (var item in live)
            {
                cached[item.Key] = item.Value;
                _cache.Set(BuildKey(origin, misses[item.Key], item.Key), item.Value, item.Value.EtaMinutes > 0 ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(5));
            }
        }

        return cached;
    }

    private static string BuildKey((double Lat, double Lng) origin, (double Lat, double Lng) destination, string id)
        => $"{id}:{origin.Lat:F6}:{origin.Lng:F6}:{destination.Lat:F6}:{destination.Lng:F6}".ToLowerInvariant();
}
