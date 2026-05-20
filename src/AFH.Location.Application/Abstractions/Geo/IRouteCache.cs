namespace AFH.Location.Application.Abstractions.Geo;

public sealed record RouteCacheEntry(RouteResult Result, TimeSpan Ttl);

public interface IRouteCache
{
    bool TryGet(string key, out RouteResult result);
    void Set(string key, RouteResult result, TimeSpan ttl);

    Task<IReadOnlyDictionary<string, RouteResult>> TryGetManyAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken ct)
    {
        var results = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TryGet(key, out var route))
                results[key] = route;
        }

        return Task.FromResult<IReadOnlyDictionary<string, RouteResult>>(results);
    }

    Task SetManyAsync(
        IReadOnlyDictionary<string, RouteCacheEntry> entries,
        CancellationToken ct)
    {
        foreach (var entry in entries)
            Set(entry.Key, entry.Value.Result, entry.Value.Ttl);

        return Task.CompletedTask;
    }
}
