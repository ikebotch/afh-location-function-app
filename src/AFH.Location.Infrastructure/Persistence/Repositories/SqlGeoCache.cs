using AFH.Location.Application.Abstractions.Advisers;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class SqlGeoCache : IGeoCache, IAdviserGeoCache
{
    private readonly IDbContextFactory<LocationPolicyDbContext> _dbContextFactory;

    public SqlGeoCache(IDbContextFactory<LocationPolicyDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public bool TryGet(string key, out (double Lat, double Lng) coords)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var entry = db.GeoCacheEntries.AsNoTracking().FirstOrDefault(x => x.CacheKey == key);
        if (entry is null || entry.ExpiresUtc <= DateTime.UtcNow)
        {
            coords = default;
            return false;
        }

        coords = (entry.Latitude, entry.Longitude);
        return true;
    }

    public void Set(string key, (double Lat, double Lng) coords, TimeSpan ttl)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var entry = db.GeoCacheEntries.FirstOrDefault(x => x.CacheKey == key);
        if (entry is null)
        {
            entry = new GeoCacheEntryEntity { CacheKey = key };
            db.GeoCacheEntries.Add(entry);
        }

        entry.Latitude = coords.Lat;
        entry.Longitude = coords.Lng;
        entry.UpdatedUtc = DateTime.UtcNow;
        entry.ExpiresUtc = DateTime.UtcNow.Add(ttl <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : ttl);
        db.SaveChanges();
    }
}