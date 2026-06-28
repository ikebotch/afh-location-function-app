using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Domain;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class SqlRouteMatrixPolicyProvider : IRouteMatrixPolicyProvider
{
    private const string MaxOriginsKey = "RouteMatrix.MaxOriginsPerCall";
    private const string MaxDestinationsKey = "RouteMatrix.MaxDestinationsPerCall";
    private const string SuccessConfidenceKey = "RouteMatrix.SuccessConfidence";
    private const string FailureConfidenceKey = "RouteMatrix.FailureConfidence";
    private const string SuccessCacheTtlKey = "RouteCache.SuccessTtlMinutes";
    private const string FailureCacheTtlKey = "RouteCache.FailureTtlMinutes";
    private readonly IDbContextFactory<LocationPolicyDbContext> _dbContextFactory;

    public SqlRouteMatrixPolicyProvider(IDbContextFactory<LocationPolicyDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<RouteMatrixPolicy> GetAsync(CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var values = await db.PolicySettings
            .AsNoTracking()
            .Where(x =>
                x.Key == MaxOriginsKey ||
                x.Key == MaxDestinationsKey ||
                x.Key == SuccessConfidenceKey ||
                x.Key == FailureConfidenceKey ||
                x.Key == SuccessCacheTtlKey ||
                x.Key == FailureCacheTtlKey)
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, ct);

        return new RouteMatrixPolicy
        {
            MaxOriginsPerCall = ReadPositiveInt(values, MaxOriginsKey, 50),
            MaxDestinationsPerCall = ReadPositiveInt(values, MaxDestinationsKey, 50),
            SuccessConfidence = ReadString(values, SuccessConfidenceKey, "High"),
            FailureConfidence = ReadString(values, FailureConfidenceKey, "Low"),
            SuccessCacheTtl = TimeSpan.FromMinutes(ReadPositiveInt(values, SuccessCacheTtlKey, 30)),
            FailureCacheTtl = TimeSpan.FromMinutes(ReadPositiveInt(values, FailureCacheTtlKey, 5))
        };
    }

    private static int ReadPositiveInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
    {
        if (!values.TryGetValue(key, out var raw) || !int.TryParse(raw, out var value) || value <= 0)
            return fallback;

        return value;
    }

    private static string ReadString(IReadOnlyDictionary<string, string> values, string key, string fallback)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
}
