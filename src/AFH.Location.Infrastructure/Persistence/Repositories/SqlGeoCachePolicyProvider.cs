using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Domain;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class SqlGeoCachePolicyProvider : IGeoCachePolicyProvider
{
    private const string SuccessTtlKey = "GeoCache.SuccessTtlMinutes";
    private const string FailureTtlKey = "GeoCache.FailureTtlMinutes";
    private readonly IDbContextFactory<LocationPolicyDbContext> _dbContextFactory;

    public SqlGeoCachePolicyProvider(IDbContextFactory<LocationPolicyDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<GeoCachePolicy> GetAsync(CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var values = await db.PolicySettings
            .AsNoTracking()
            .Where(x => x.Key == SuccessTtlKey || x.Key == FailureTtlKey)
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, ct);

        var successTtl = ReadPositiveMinutes(values, SuccessTtlKey, 7 * 24 * 60);
        var failureTtl = ReadPositiveMinutes(values, FailureTtlKey, 30);

        return new GeoCachePolicy
        {
            AdviserHomeTtl = successTtl,
            AdviserOfficeTtl = successTtl,
            DestinationTtl = successTtl,
            SuccessTtl = successTtl,
            FailureTtl = failureTtl
        };
    }

    private static TimeSpan ReadPositiveMinutes(
        IReadOnlyDictionary<string, string> values,
        string key,
        int fallbackMinutes)
    {
        if (!values.TryGetValue(key, out var raw) || !int.TryParse(raw, out var minutes) || minutes <= 0)
            minutes = fallbackMinutes;

        return TimeSpan.FromMinutes(minutes);
    }
}
