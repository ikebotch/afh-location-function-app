using AFH.Location.Application.Abstractions;

namespace AFH.Location.Infrastructure.External.Maps;

public sealed class CachedRoutingService : IRoutingService
{
    private readonly IRoutingService _inner;
    private readonly IRouteCache _cache;

    public CachedRoutingService(IRoutingService inner, IRouteCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<RouteResult> GetRouteAsync((double Lat, double Lng) origin, (double Lat, double Lng) destination, CancellationToken ct)
    {
        var key = BuildKey(origin, destination, "single");
        if (_cache.TryGet(key, out var cached))
            return cached;

        var live = await _inner.GetRouteAsync(origin, destination, ct);
        var ttl = live.EtaMinutes > 0 ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(5);
        _cache.Set(key, live, ttl);
        return live;
    }

    private static string BuildKey((double Lat, double Lng) origin, (double Lat, double Lng) destination, string suffix)
        => $"{suffix}:{origin.Lat:F6}:{origin.Lng:F6}:{destination.Lat:F6}:{destination.Lng:F6}".ToLowerInvariant();
}
