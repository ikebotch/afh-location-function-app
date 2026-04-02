using AFH.Location.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace AFH.Location.Infrastructure.Caching;

public sealed class InMemoryGeoCache : IGeoCache
{
    private readonly IMemoryCache _cache;

    public InMemoryGeoCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryGet(string key, out (double Lat, double Lng) coords)
        => _cache.TryGetValue(key, out coords);

    public void Set(string key, (double Lat, double Lng) coords, TimeSpan ttl)
    {
        // Guard: MemoryCache requires a positive value
        if (ttl <= TimeSpan.Zero)
            ttl = TimeSpan.FromMinutes(10);

        _cache.Set(key, coords, ttl);
    }
}