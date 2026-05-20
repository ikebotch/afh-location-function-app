using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Domain.Travel;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

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

        result = new RouteResult(
            entry.EtaMinutes,
            entry.DistanceMiles,
            entry.Confidence,
            TravelRouteResolutionSource.Database);
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

    public async Task<IReadOnlyDictionary<string, RouteResult>> TryGetManyAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken ct)
    {
        var distinctKeys = keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctKeys.Length == 0)
            return new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var entries = await db.RouteCacheEntries
            .AsNoTracking()
            .Where(x => distinctKeys.Contains(x.CacheKey) && x.ExpiresUtc > now)
            .ToListAsync(ct);

        return entries.ToDictionary(
            entry => entry.CacheKey,
            entry => new RouteResult(
                entry.EtaMinutes,
                entry.DistanceMiles,
                entry.Confidence,
                TravelRouteResolutionSource.Database),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task SetManyAsync(
        IReadOnlyDictionary<string, RouteCacheEntry> entries,
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
        var existing = await db.RouteCacheEntries
            .Where(x => keys.Contains(x.CacheKey))
            .ToDictionaryAsync(x => x.CacheKey, StringComparer.OrdinalIgnoreCase, ct);

        var now = DateTime.UtcNow;
        foreach (var item in distinctEntries)
        {
            if (!existing.TryGetValue(item.Key, out var entity))
            {
                entity = new RouteCacheEntryEntity { CacheKey = item.Key };
                db.RouteCacheEntries.Add(entity);
            }

            entity.EtaMinutes = item.Value.Result.EtaMinutes;
            entity.DistanceMiles = item.Value.Result.DistanceMiles;
            entity.Confidence = item.Value.Result.Confidence;
            entity.UpdatedUtc = now;
            entity.ExpiresUtc = now.Add(item.Value.Ttl <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : item.Value.Ttl);
        }

        await db.SaveChangesAsync(ct);
    }
}
