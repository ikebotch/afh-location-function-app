using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Domain;
using AFH.Location.Domain.Travel;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AFH.Location.Infrastructure.External.Maps;

public sealed class CachedRouteMatrixService : IRouteMatrixService
{
    private readonly IRouteMatrixService _inner;
    private readonly IRouteCache _cache;
    private readonly IRouteMatrixPolicyProvider _policyProvider;
    private readonly ILogger<CachedRouteMatrixService>? _logger;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RouteResult> _requestCache = new(StringComparer.OrdinalIgnoreCase);

    public CachedRouteMatrixService(
        IRouteMatrixService inner,
        IRouteCache cache,
        IRouteMatrixPolicyProvider policyProvider,
        ILogger<CachedRouteMatrixService>? logger = null)
    {
        _inner = inner;
        _cache = cache;
        _policyProvider = policyProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
        IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
        (double Lat, double Lng) destination,
        CancellationToken ct)
    {
        var policy = await _policyProvider.GetAsync(ct);
        var cached = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);
        var misses = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);

        foreach (var adviser in adviserOrigins)
        {
            if (TryGetCachedRoute(adviser.Value, destination, adviser.Key, policy, out var route))
                cached[adviser.Key] = route;
            else
                misses[adviser.Key] = adviser.Value;
        }

        if (misses.Count > 0)
        {
            LogCacheSummary("AdviserToDestination", cached.Count, misses.Count, 1);
            var live = await _inner.GetAdviserToDestinationAsync(misses, destination, ct);
            foreach (var item in live)
            {
                cached[item.Key] = item.Value;
                if (!IsSyntheticFallback(item.Value) && misses.TryGetValue(item.Key, out var adviserCoords))
                    CacheRoute(adviserCoords, destination, item.Key, item.Value, policy);
            }
        }
        else
        {
            LogCacheSummary("AdviserToDestination", cached.Count, 0, 0);
        }

        return cached;
    }

    public async Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
        (double Lat, double Lng) origin,
        IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
        DateTimeOffset? departAt = null,
        CancellationToken ct = default)
    {
        var policy = await _policyProvider.GetAsync(ct);
        var cached = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);
        var misses = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);
        var lookupKeys = new Dictionary<string, RouteCacheLookupKeys>(StringComparer.OrdinalIgnoreCase);
        var bulkCacheKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cacheWrites = new Dictionary<string, RouteCacheEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var destination in destinations)
        {
            var keys = new RouteCacheLookupKeys(
                BuildKey(origin, destination.Value, destination.Key, departAt),
                BuildSingleKey(origin, destination.Value, departAt));
            lookupKeys[destination.Key] = keys;

            if (_requestCache.TryGetValue(keys.SpecificKey, out var route) ||
                _requestCache.TryGetValue(keys.SingleKey, out route!))
            {
                _logger?.LogInformation("Request-scoped route cache hit. SpecificKey={SpecificKey}", keys.SpecificKey);
                cached[destination.Key] = route;
                _requestCache[keys.SpecificKey] = route;
                continue;
            }

            bulkCacheKeys.Add(keys.SpecificKey);
            bulkCacheKeys.Add(keys.SingleKey);
            misses[destination.Key] = destination.Value;
        }

        if (bulkCacheKeys.Count > 0)
        {
            var cacheReadStopwatch = Stopwatch.StartNew();
            var cacheHits = await _cache.TryGetManyAsync(bulkCacheKeys.ToArray(), ct);
            cacheReadStopwatch.Stop();
            LogPhaseTiming("RouteCacheRead", cacheReadStopwatch.ElapsedMilliseconds, bulkCacheKeys.Count, cacheHits.Count);

            foreach (var miss in misses.ToList())
            {
                var keys = lookupKeys[miss.Key];
                if (cacheHits.TryGetValue(keys.SpecificKey, out var route) ||
                    cacheHits.TryGetValue(keys.SingleKey, out route!))
                {
                    cached[miss.Key] = route;
                    _requestCache[keys.SpecificKey] = route;
                    _requestCache[keys.SingleKey] = route;
                    misses.Remove(miss.Key);

                    if (cacheHits.ContainsKey(keys.SingleKey) && !cacheHits.ContainsKey(keys.SpecificKey))
                        cacheWrites[keys.SpecificKey] = new RouteCacheEntry(route, GetTtl(route, policy));
                }
            }
        }
        else
        {
            LogPhaseTiming("RouteCacheRead", 0, 0, 0);
        }

        if (misses.Count > 0)
        {
            LogCacheSummary("OneToMany", cached.Count, misses.Count, 1);
            var azureStopwatch = Stopwatch.StartNew();
            var live = await _inner.GetOneToManyAsync(origin, misses, departAt, ct);
            azureStopwatch.Stop();
            LogPhaseTiming("AzureMatrix", azureStopwatch.ElapsedMilliseconds, misses.Count, live.Count);

            foreach (var item in live)
            {
                cached[item.Key] = item.Value;

                // Only persist genuine provider results, not synthetic fallback zeros.
                // A real "Low" confidence result with 0 minutes and 0 miles means the
                // provider had no route data for this pair — do not cache it, otherwise
                // a transient provider failure poisons the cache for the failure TTL window.
                if (!IsSyntheticFallback(item.Value) && misses.TryGetValue(item.Key, out var destCoords))
                    AddCacheRouteWrite(origin, destCoords, item.Key, item.Value, departAt, policy, cacheWrites);
            }

            // Destinations not returned by the provider at all get the fallback.
            // These are never written to cache.
            foreach (var miss in misses)
            {
                if (!cached.ContainsKey(miss.Key))
                    cached[miss.Key] = new RouteResult(0, 0, "Low", TravelRouteResolutionSource.AzureMaps);
            }
        }
        else
        {
            LogCacheSummary("OneToMany", cached.Count, 0, 0);
            LogPhaseTiming("AzureMatrix", 0, 0, 0);
        }

        if (cacheWrites.Count > 0)
        {
            var cacheWriteStopwatch = Stopwatch.StartNew();
            await _cache.SetManyAsync(cacheWrites, ct);
            cacheWriteStopwatch.Stop();
            LogPhaseTiming("RouteCacheWrite", cacheWriteStopwatch.ElapsedMilliseconds, cacheWrites.Count, cacheWrites.Count);
        }
        else
        {
            LogPhaseTiming("RouteCacheWrite", 0, 0, 0);
        }

        return cached;
    }

    private bool TryGetCachedRoute(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        string id,
        RouteMatrixPolicy policy,
        out RouteResult route)
    {
        return TryGetCachedRoute(origin, destination, id, null, policy, out route);
    }

    private bool TryGetCachedRoute(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        string id,
        DateTimeOffset? departAt,
        RouteMatrixPolicy policy,
        out RouteResult route)
    {
        var specificKey = BuildKey(origin, destination, id, departAt);
        if (_requestCache.TryGetValue(specificKey, out route!))
            return true;

        if (_cache.TryGet(specificKey, out route))
        {
            _requestCache[specificKey] = route;
            return true;
        }

        var singleKey = BuildSingleKey(origin, destination, departAt);
        if (_requestCache.TryGetValue(singleKey, out route!))
        {
            _requestCache[specificKey] = route;
            return true;
        }

        if (_cache.TryGet(singleKey, out route))
        {
            _requestCache[specificKey] = route;
            _requestCache[singleKey] = route;
            _cache.Set(specificKey, route, GetTtl(route, policy));
            return true;
        }

        route = default!;
        return false;
    }

    private void CacheRoute(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        string id,
        RouteResult route,
        RouteMatrixPolicy policy,
        DateTimeOffset? departAt = null)
    {
        var cacheWrites = new Dictionary<string, RouteCacheEntry>(StringComparer.OrdinalIgnoreCase);
        AddCacheRouteWrite(origin, destination, id, route, departAt, policy, cacheWrites);

        foreach (var item in cacheWrites)
            _cache.Set(item.Key, item.Value.Result, item.Value.Ttl);
    }

    private void AddCacheRouteWrite(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        string id,
        RouteResult route,
        DateTimeOffset? departAt,
        RouteMatrixPolicy policy,
        IDictionary<string, RouteCacheEntry> cacheWrites)
    {
        var ttl = GetTtl(route, policy);
        var specificKey = BuildKey(origin, destination, id, departAt);
        var singleKey = BuildSingleKey(origin, destination, departAt);

        _requestCache[specificKey] = route;
        _requestCache[singleKey] = route;

        cacheWrites[specificKey] = new RouteCacheEntry(route, ttl);
        cacheWrites[singleKey] = new RouteCacheEntry(route, ttl);
    }

    private static TimeSpan GetTtl(RouteResult route, RouteMatrixPolicy policy)
        => route.EtaMinutes > 0 ? policy.SuccessCacheTtl : policy.FailureCacheTtl;

    /// <summary>
    /// Returns true when the result is the synthetic fallback that the provider
    /// inserts for destinations it has no data for (EtaMinutes=0, DistanceMiles=0,
    /// Confidence="Low"). These must not be persisted to cache.
    /// </summary>
    private static bool IsSyntheticFallback(RouteResult result)
        => result.EtaMinutes == 0
           && result.DistanceMiles == 0d
           && string.Equals(result.Confidence, "Low", StringComparison.OrdinalIgnoreCase);

    private static string BuildKey((double Lat, double Lng) origin, (double Lat, double Lng) destination, string id, DateTimeOffset? departAt)
    {
        var baseKey = $"{id}:{origin.Lat:F6}:{origin.Lng:F6}:{destination.Lat:F6}:{destination.Lng:F6}";
        if (departAt.HasValue)
        {
            baseKey += $":{departAt.Value.ToString("yyyyMMddHHmm")}";
        }
        return baseKey.ToLowerInvariant();
    }

    private static string BuildSingleKey((double Lat, double Lng) origin, (double Lat, double Lng) destination, DateTimeOffset? departAt)
    {
        var baseKey = $"single:{origin.Lat:F6}:{origin.Lng:F6}:{destination.Lat:F6}:{destination.Lng:F6}";
        if (departAt.HasValue)
        {
            baseKey += $":{departAt.Value.ToString("yyyyMMddHHmm")}";
        }
        return baseKey.ToLowerInvariant();
    }

    private void LogCacheSummary(string direction, int hitCount, int missCount, int providerCallCount)
    {
        _logger?.LogInformation(
            "Location route matrix cache summary. Direction={Direction} HitCount={HitCount} MissCount={MissCount} ProviderCallCount={ProviderCallCount}",
            direction,
            hitCount,
            missCount,
            providerCallCount);
    }

    private void LogPhaseTiming(string phase, long durationMs, int itemCount, int resultCount)
    {
        _logger?.LogInformation(
            "Location travel coverage phase timing. Phase={Phase} DurationMs={DurationMs} ItemCount={ItemCount} ResultCount={ResultCount}",
            phase,
            durationMs,
            itemCount,
            resultCount);
    }

    private sealed record RouteCacheLookupKeys(string SpecificKey, string SingleKey);
}
