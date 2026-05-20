using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Application.Models.V1.Travel;

namespace AFH.Location.Infrastructure.External.Maps;

public sealed class PostcodeCoordinateResolver : IPostcodeCoordinateResolver
{
    private readonly IGeoCache _cache;
    private readonly IGeocodingService _geocoding;
    private readonly IGeoCachePolicyProvider _policyProvider;

    public PostcodeCoordinateResolver(
        IGeoCache cache,
        IGeocodingService geocoding,
        IGeoCachePolicyProvider policyProvider)
    {
        _cache = cache;
        _geocoding = geocoding;
        _policyProvider = policyProvider;
    }

    public async Task<PostcodeCoordinateResolution> ResolveAsync(string postcode, CancellationToken ct)
    {
        var normalisedPostcode = NormalisePostcode(postcode);
        if (string.IsNullOrWhiteSpace(normalisedPostcode))
            return new PostcodeCoordinateResolution { Postcode = string.Empty };

        var cacheKey = BuildCacheKey(normalisedPostcode);
        if (_cache.TryGet(cacheKey, out var cached))
        {
            return new PostcodeCoordinateResolution
            {
                Postcode = normalisedPostcode,
                Coordinates = IsUsable(cached)
                    ? new LocationCoordinates(cached.Lat, cached.Lng)
                    : null
            };
        }

        var policy = await _policyProvider.GetAsync(ct);
        var resolved = await _geocoding.GeocodeAsync($"{normalisedPostcode}, United Kingdom", ct);
        var ttl = IsUsable(resolved)
            ? policy.DestinationTtl > TimeSpan.Zero ? policy.DestinationTtl : policy.SuccessTtl
            : policy.FailureTtl;

        _cache.Set(cacheKey, resolved, ttl);

        return new PostcodeCoordinateResolution
        {
            Postcode = normalisedPostcode,
            Coordinates = IsUsable(resolved)
                ? new LocationCoordinates(resolved.Lat, resolved.Lng)
                : null
        };
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
}
