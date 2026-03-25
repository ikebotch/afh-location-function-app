using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

public sealed class SqlGeoCache : IGeoCache, IAdviserGeoCache
{
    private readonly LocationPolicyDbContext _db;

    public SqlGeoCache(LocationPolicyDbContext db)
    {
        _db = db;
    }

    public bool TryGet(string key, out (double Lat, double Lng) coords)
    {
        var entry = _db.GeoCacheEntries.FirstOrDefault(x => x.CacheKey == key);
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
        var entry = _db.GeoCacheEntries.FirstOrDefault(x => x.CacheKey == key);
        if (entry is null)
        {
            entry = new GeoCacheEntryEntity { CacheKey = key };
            _db.GeoCacheEntries.Add(entry);
        }

        entry.Latitude = coords.Lat;
        entry.Longitude = coords.Lng;
        entry.UpdatedUtc = DateTime.UtcNow;
        entry.ExpiresUtc = DateTime.UtcNow.Add(ttl <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : ttl);
        _db.SaveChanges();
    }
}
