using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Abstractions.Geo;

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
            if (TryGetCachedRoute(adviser.Value, destination, adviser.Key, out var route))
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
                CacheRoute(misses[item.Key], destination, item.Key, item.Value);
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
            if (TryGetCachedRoute(origin, destination.Value, destination.Key, out var route))
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
                CacheRoute(origin, misses[item.Key], item.Key, item.Value);
            }
        }

        return cached;
    }

    private bool TryGetCachedRoute(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        string id,
        out RouteResult route)
    {
        var specificKey = BuildKey(origin, destination, id);
        if (_cache.TryGet(specificKey, out route))
            return true;

        var singleKey = BuildSingleKey(origin, destination);
        if (_cache.TryGet(singleKey, out route))
        {
            _cache.Set(specificKey, route, GetTtl(route));
            return true;
        }

        route = default!;
        return false;
    }

    private void CacheRoute(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        string id,
        RouteResult route)
    {
        var ttl = GetTtl(route);
        _cache.Set(BuildKey(origin, destination, id), route, ttl);
        _cache.Set(BuildSingleKey(origin, destination), route, ttl);
    }

    private static TimeSpan GetTtl(RouteResult route)
        => route.EtaMinutes > 0 ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(5);

    private static string BuildKey((double Lat, double Lng) origin, (double Lat, double Lng) destination, string id)
        => $"{id}:{origin.Lat:F6}:{origin.Lng:F6}:{destination.Lat:F6}:{destination.Lng:F6}".ToLowerInvariant();

    private static string BuildSingleKey((double Lat, double Lng) origin, (double Lat, double Lng) destination)
        => $"single:{origin.Lat:F6}:{origin.Lng:F6}:{destination.Lat:F6}:{destination.Lng:F6}".ToLowerInvariant();
}