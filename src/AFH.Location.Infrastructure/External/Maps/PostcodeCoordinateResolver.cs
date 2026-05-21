using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Application.Models.Travel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AFH.Location.Infrastructure.External.Maps;

public sealed class PostcodeCoordinateResolver : IPostcodeCoordinateResolver
{
    private readonly IGeoCache _cache;
    private readonly IGeocodingService _geocoding;
    private readonly IGeoCachePolicyProvider _policyProvider;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<PostcodeCoordinateResolver>? _logger;

    public PostcodeCoordinateResolver(
        IGeoCache cache,
        IGeocodingService geocoding,
        IGeoCachePolicyProvider policyProvider,
        IConfiguration? configuration = null,
        ILogger<PostcodeCoordinateResolver>? logger = null)
    {
        _cache = cache;
        _geocoding = geocoding;
        _policyProvider = policyProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PostcodeCoordinateResolution> ResolveAsync(string postcode, CancellationToken ct)
    {
        var results = await ResolveManyAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [NormalisePostcode(postcode)] = postcode
            },
            ct);

        return results.Values.FirstOrDefault()
            ?? new PostcodeCoordinateResolution { Postcode = NormalisePostcode(postcode) };
    }

    public async Task<IReadOnlyDictionary<string, PostcodeCoordinateResolution>> ResolveManyAsync(
        IReadOnlyDictionary<string, string> postcodesByKey,
        CancellationToken ct)
    {
        var requested = postcodesByKey
            .Select(item => new PostcodeLookup(item.Key, NormalisePostcode(item.Value)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Postcode))
            .ToList();

        var results = postcodesByKey.ToDictionary(
            item => item.Key,
            item => new PostcodeCoordinateResolution { Postcode = NormalisePostcode(item.Value) },
            StringComparer.OrdinalIgnoreCase);

        if (requested.Count == 0)
            return results;

        var cacheKeyByPostcode = requested
            .Select(item => item.Postcode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(postcode => postcode, BuildCacheKey, StringComparer.OrdinalIgnoreCase);

        var cacheReadStopwatch = Stopwatch.StartNew();
        var cached = await _cache.TryGetManyAsync(cacheKeyByPostcode.Values.ToArray(), ct);
        cacheReadStopwatch.Stop();
        LogPhaseTiming(
            "PostcodeCacheRead",
            cacheReadStopwatch.ElapsedMilliseconds,
            requested.Count,
            cached.Count);

        foreach (var item in requested)
        {
            var cacheKey = cacheKeyByPostcode[item.Postcode];
            if (cached.TryGetValue(cacheKey, out var coords))
                results[item.Key] = BuildResolution(item.Postcode, coords);
        }

        var missedPostcodes = requested
            .Select(item => item.Postcode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(postcode => !cached.ContainsKey(cacheKeyByPostcode[postcode]))
            .ToList();

        if (missedPostcodes.Count == 0)
        {
            LogPhaseTiming("GeocodeProvider", 0, 0, 0);
            return results;
        }

        var policy = await _policyProvider.GetAsync(ct);
        var geocodeStopwatch = Stopwatch.StartNew();
        var providerResults = await GeocodeMissesAsync(missedPostcodes, ct);
        geocodeStopwatch.Stop();
        LogPhaseTiming(
            "GeocodeProvider",
            geocodeStopwatch.ElapsedMilliseconds,
            missedPostcodes.Count,
            providerResults.Count);

        var cacheWrites = new Dictionary<string, GeoCacheEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in providerResults)
        {
            var ttl = IsUsable(item.Value)
                ? policy.DestinationTtl > TimeSpan.Zero ? policy.DestinationTtl : policy.SuccessTtl
                : policy.FailureTtl;

            cacheWrites[cacheKeyByPostcode[item.Key]] = new GeoCacheEntry(item.Value, ttl);
        }

        if (cacheWrites.Count > 0)
            await _cache.SetManyAsync(cacheWrites, ct);

        foreach (var item in requested)
        {
            if (providerResults.TryGetValue(item.Postcode, out var coords))
                results[item.Key] = BuildResolution(item.Postcode, coords);
        }

        return results;
    }

    private static string BuildCacheKey(string postcode)
        => $"postcode:coord:v1:{postcode.Replace(" ", string.Empty, StringComparison.Ordinal)}".ToLowerInvariant();

    private static string NormalisePostcode(string? postcode)
        => string.Join(
            " ",
            (postcode ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static bool IsUsable((double Lat, double Lng) coordinates)
        => coordinates is not { Lat: 0d, Lng: 0d };

    private async Task<IReadOnlyDictionary<string, (double Lat, double Lng)>> GeocodeMissesAsync(
        IReadOnlyCollection<string> postcodes,
        CancellationToken ct)
    {
        var maxParallelism = GetConfiguredPositiveInt("TravelCoverage:PostcodeResolutionMaxDegreeOfParallelism", 8);
        using var semaphore = new SemaphoreSlim(maxParallelism);

        var tasks = postcodes.Select(async postcode =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var coordinates = await _geocoding.GeocodeAsync($"{postcode}, United Kingdom", ct);
                return (Postcode: postcode, Coordinates: coordinates);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var resolved = await Task.WhenAll(tasks);
        return resolved.ToDictionary(
            item => item.Postcode,
            item => item.Coordinates,
            StringComparer.OrdinalIgnoreCase);
    }

    private int GetConfiguredPositiveInt(string key, int defaultValue)
    {
        if (_configuration == null)
            return defaultValue;

        var value = _configuration.GetSection(key)?.Value;
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private void LogPhaseTiming(string phase, long durationMs, int itemCount, int resultCount)
    {
        _logger?.LogInformation(
            "Location travel coverage phase timing. Phase={Phase} DurationMs={DurationMs} ItemCount={ItemCount} ResultCount={ResultCount}",
            phase,
            durationMs,
            itemCount,
            resultCount);
    }

    private static PostcodeCoordinateResolution BuildResolution(
        string postcode,
        (double Lat, double Lng) coordinates)
        => new()
        {
            Postcode = postcode,
            Coordinates = IsUsable(coordinates)
                ? new LocationCoordinates(coordinates.Lat, coordinates.Lng)
                : null
        };

    private sealed record PostcodeLookup(string Key, string Postcode);
}
