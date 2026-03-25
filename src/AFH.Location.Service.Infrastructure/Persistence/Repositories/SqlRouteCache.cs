using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

public sealed class SqlRouteCache : IRouteCache
{
    private readonly LocationPolicyDbContext _db;

    public SqlRouteCache(LocationPolicyDbContext db)
    {
        _db = db;
    }

    public bool TryGet(string key, out RouteResult result)
    {
        var entry = _db.RouteCacheEntries.FirstOrDefault(x => x.CacheKey == key);
        if (entry is null || entry.ExpiresUtc <= DateTime.UtcNow)
        {
            result = default!;
            return false;
        }

        result = new RouteResult(entry.EtaMinutes, entry.DistanceMiles, entry.Confidence);
        return true;
    }

    public void Set(string key, RouteResult result, TimeSpan ttl)
    {
        var entry = _db.RouteCacheEntries.FirstOrDefault(x => x.CacheKey == key);
        if (entry is null)
        {
            entry = new RouteCacheEntryEntity { CacheKey = key };
            _db.RouteCacheEntries.Add(entry);
        }

        entry.EtaMinutes = result.EtaMinutes;
        entry.DistanceMiles = result.DistanceMiles;
        entry.Confidence = result.Confidence;
        entry.UpdatedUtc = DateTime.UtcNow;
        entry.ExpiresUtc = DateTime.UtcNow.Add(ttl <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : ttl);
        _db.SaveChanges();
    }
}
