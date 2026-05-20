namespace AFH.Location.Application.Abstractions.Geo;

public sealed record GeoCacheEntry((double Lat, double Lng) Coordinates, TimeSpan Ttl);

public interface IGeoCache
{
    bool TryGet(string key, out (double Lat, double Lng) coords);
    void Set(string key, (double Lat, double Lng) coords, TimeSpan ttl);

    Task<IReadOnlyDictionary<string, (double Lat, double Lng)>> TryGetManyAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken ct)
    {
        var results = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TryGet(key, out var coords))
                results[key] = coords;
        }

        return Task.FromResult<IReadOnlyDictionary<string, (double Lat, double Lng)>>(results);
    }

    Task SetManyAsync(
        IReadOnlyDictionary<string, GeoCacheEntry> entries,
        CancellationToken ct)
    {
        foreach (var entry in entries)
            Set(entry.Key, entry.Value.Coordinates, entry.Value.Ttl);

        return Task.CompletedTask;
    }
}
