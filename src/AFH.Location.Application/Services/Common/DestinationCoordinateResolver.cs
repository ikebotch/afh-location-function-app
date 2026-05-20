using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Models.V1.Requests;
using AFH.Location.Domain;
using Microsoft.Extensions.Logging;

namespace AFH.Location.Application.Services.Common;

public sealed class DestinationCoordinateResolver
{
    private readonly IGeoCache _cache;
    private readonly IGeocodingService _geocoding;
    private readonly IGeoCachePolicyProvider _policyProvider;
    private readonly ILogger<DestinationCoordinateResolver>? _logger;

    public DestinationCoordinateResolver(
        IGeoCache cache,
        IGeocodingService geocoding,
        IGeoCachePolicyProvider policyProvider,
        ILogger<DestinationCoordinateResolver>? logger = null)
    {
        _cache = cache;
        _geocoding = geocoding;
        _policyProvider = policyProvider;
        _logger = logger;
    }

    public async Task<DestinationResolved> ResolveAsync(SearchDestination destination, CancellationToken ct)
    {
        if (destination.Coordinates is not null)
        {
            return new DestinationResolved
            {
                Lat = destination.Coordinates.Lat,
                Lng = destination.Coordinates.Lng,
                Source = DestinationSource.Coordinates
            };
        }

        if (destination.Address is not null)
        {
            var normalisedAddress = FormatAddress(destination.Address);
            var policy = await _policyProvider.GetAsync(ct);
            var cacheKey = BuildCacheKey(destination.Address, normalisedAddress);

            if (_cache.TryGet(cacheKey, out var cached))
            {
                _logger?.LogInformation(
                    "Location geocode cache hit. CacheKey={CacheKey} Postcode={Postcode}",
                    cacheKey,
                    destination.Address.Postcode);

                return new DestinationResolved
                {
                    Lat = cached.Lat,
                    Lng = cached.Lng,
                    Source = DestinationSource.GeocodedAddress,
                    NormalisedAddress = normalisedAddress
                };
            }

            _logger?.LogInformation(
                "Location geocode cache miss. CacheKey={CacheKey} Postcode={Postcode}",
                cacheKey,
                destination.Address.Postcode);

            var (lat, lng) = await _geocoding.GeocodeAsync(normalisedAddress, ct);
            var ttl = lat == 0d && lng == 0d
                ? policy.FailureTtl
                : policy.DestinationTtl > TimeSpan.Zero
                    ? policy.DestinationTtl
                    : policy.SuccessTtl;

            _cache.Set(cacheKey, (lat, lng), ttl);

            return new DestinationResolved
            {
                Lat = lat,
                Lng = lng,
                Source = DestinationSource.GeocodedAddress,
                NormalisedAddress = normalisedAddress
            };
        }

        throw new InvalidOperationException("Destination must contain coordinates or address.");
    }

    private static string BuildCacheKey(SearchAddress address, string normalisedAddress)
    {
        if (!string.IsNullOrWhiteSpace(address.Postcode))
        {
            var postcode = address.Postcode
                .Trim()
                .ToUpperInvariant()
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            return $"destination:postcode:v1:{postcode}".ToLowerInvariant();
        }

        return $"destination:address:v1:{normalisedAddress.Trim().ToUpperInvariant()}".ToLowerInvariant();
    }

    private static string FormatAddress(SearchAddress address)
    {
        return string.Join(", ", new[]
        {
            address.Line1,
            address.Line2,
            address.Town,
            address.Postcode,
            address.Country
        }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
    }
}