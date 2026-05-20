using AFH.Location.Application.Abstractions.Geo;
using Microsoft.Extensions.Logging;

namespace AFH.Location.Infrastructure.External.Maps;

public sealed class CachedRoutingService : IRoutingService
{
    private readonly IRoutingService _inner;
    private readonly IRouteCache _cache;
    private readonly ILogger<CachedRoutingService>? _logger;

    public CachedRoutingService(
        IRoutingService inner,
        IRouteCache cache,
        ILogger<CachedRoutingService>? logger = null)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RouteResult> GetRouteAsync(
     (double Lat, double Lng) origin,
     (double Lat, double Lng) destination,
     CancellationToken ct)
    {
        var key = BuildKey(origin, destination, "single");

        if (_cache.TryGet(key, out var cached) && IsUsable(cached))
        {
            _logger?.LogInformation(
                "Location route cache hit. CacheKey={CacheKey} ProviderCallCount={ProviderCallCount}",
                key,
                0);
            return cached;
        }

        _logger?.LogInformation(
            "Location route cache miss. CacheKey={CacheKey} ProviderCallCount={ProviderCallCount}",
            key,
            1);

        var live = await _inner.GetRouteAsync(origin, destination, ct);

        if (IsUsable(live))
        {
            _cache.Set(key, live, TimeSpan.FromMinutes(30));
        }

        return live;
    }

    private static bool IsUsable(RouteResult route) =>
        route.EtaMinutes > 0 && route.DistanceMiles > 0;

    private static string BuildKey((double Lat, double Lng) origin, (double Lat, double Lng) destination, string suffix)
        => $"{suffix}:{origin.Lat:F6}:{origin.Lng:F6}:{destination.Lat:F6}:{destination.Lng:F6}".ToLowerInvariant();
}