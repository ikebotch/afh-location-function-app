using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Common.Extensions;
using AFH.Location.Service.Core.Contracts.V1.Requests;

namespace AFH.Location.Service.Core.Services.Common;

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

    public async Task<DestinationResolved> ResolveAsync(Destination destination, CancellationToken ct)
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
            var (lat, lng) = await _geocoding.GeocodeAsync(destination.Address.ToSingleLine(), ct);

            return new DestinationResolved
            {
                Lat = lat,
                Lng = lng,
                Source = DestinationSource.GeocodedAddress,
                NormalisedAddress = destination.Address.ToSingleLine()
            };
        }

        throw new InvalidOperationException("Destination must contain coordinates or address.");
    }



    private static string NormaliseAddress(Address a)
    {
        // Keep it simple and deterministic
        var parts = new[]
        {
            a.Line1, a.Line2, a.Town, a.Postcode, a.Country
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim());

        return string.Join(", ", parts);
    }
}