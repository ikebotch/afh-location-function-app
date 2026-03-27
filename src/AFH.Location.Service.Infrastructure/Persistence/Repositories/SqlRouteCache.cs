using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

public sealed class SqlRouteCache : IRouteCache
{
    private readonly IDbContextFactory<LocationPolicyDbContext> _dbContextFactory;

    public SqlRouteCache(IDbContextFactory<LocationPolicyDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public bool TryGet(string key, out RouteResult result)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var entry = db.RouteCacheEntries.AsNoTracking().FirstOrDefault(x => x.CacheKey == key);
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
        using var db = _dbContextFactory.CreateDbContext();
        var entry = db.RouteCacheEntries.FirstOrDefault(x => x.CacheKey == key);
        if (entry is null)
        {
            entry = new RouteCacheEntryEntity { CacheKey = key };
            db.RouteCacheEntries.Add(entry);
        }

        entry.EtaMinutes = result.EtaMinutes;
        entry.DistanceMiles = result.DistanceMiles;
        entry.Confidence = result.Confidence;
        entry.UpdatedUtc = DateTime.UtcNow;
        entry.ExpiresUtc = DateTime.UtcNow.Add(ttl <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : ttl);
        db.SaveChanges();
    }
}
