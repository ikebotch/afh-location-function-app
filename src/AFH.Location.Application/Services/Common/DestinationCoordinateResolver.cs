using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Models.V1.Requests;
using AFH.Location.Domain;

namespace AFH.Location.Application.Services.Common;

public sealed class DestinationCoordinateResolver
{
    private readonly IGeoCache _cache;
    private readonly IGeocodingService _geocoding;
    private readonly IGeoCachePolicyProvider _policyProvider;

    public DestinationCoordinateResolver(
        IGeoCache cache,
        IGeocodingService geocoding,
        IGeoCachePolicyProvider policyProvider)
    {
        _cache = cache;
        _geocoding = geocoding;
        _policyProvider = policyProvider;
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
            var (lat, lng) = await _geocoding.GeocodeAsync(normalisedAddress, ct);

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
