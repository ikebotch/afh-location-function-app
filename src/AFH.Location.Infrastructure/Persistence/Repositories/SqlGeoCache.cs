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

    public async Task<IReadOnlyDictionary<string, (double Lat, double Lng)>> TryGetManyAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken ct)
    {
        var distinctKeys = keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctKeys.Length == 0)
            return new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var entries = await db.GeoCacheEntries
            .AsNoTracking()
            .Where(x => distinctKeys.Contains(x.CacheKey) && x.ExpiresUtc > now)
            .ToListAsync(ct);

        return entries.ToDictionary(
            entry => entry.CacheKey,
            entry => (entry.Latitude, entry.Longitude),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task SetManyAsync(
        IReadOnlyDictionary<string, GeoCacheEntry> entries,
        CancellationToken ct)
    {
        var distinctEntries = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        if (distinctEntries.Count == 0)
            return;

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var keys = distinctEntries.Keys.ToArray();
        var existing = await db.GeoCacheEntries
            .Where(x => keys.Contains(x.CacheKey))
            .ToDictionaryAsync(x => x.CacheKey, StringComparer.OrdinalIgnoreCase, ct);

        var now = DateTime.UtcNow;
        foreach (var item in distinctEntries)
        {
            if (!existing.TryGetValue(item.Key, out var entity))
            {
                entity = new GeoCacheEntryEntity { CacheKey = item.Key };
                db.GeoCacheEntries.Add(entity);
            }

            entity.Latitude = item.Value.Coordinates.Lat;
            entity.Longitude = item.Value.Coordinates.Lng;
            entity.UpdatedUtc = now;
            entity.ExpiresUtc = now.Add(item.Value.Ttl <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : item.Value.Ttl);
        }

        await db.SaveChangesAsync(ct);
    }
}
