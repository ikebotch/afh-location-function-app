using AFH.Location.Application.Abstractions.Geo;
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

    public Task<IReadOnlyDictionary<string, (double Lat, double Lng)>> TryGetManyAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken ct)
    {
        var results = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_cache.TryGetValue(key, out (double Lat, double Lng) coords))
                results[key] = coords;
        }

        return Task.FromResult<IReadOnlyDictionary<string, (double Lat, double Lng)>>(results);
    }

    public Task SetManyAsync(
        IReadOnlyDictionary<string, GeoCacheEntry> entries,
        CancellationToken ct)
    {
        foreach (var entry in entries)
            Set(entry.Key, entry.Value.Coordinates, entry.Value.Ttl);

        return Task.CompletedTask;
    }
}
