using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Models.V1.Requests;
using AFH.Location.Domain;
using AFH.Location.Domain.Errors;

namespace AFH.Location.Application.Services.Common;

public sealed class DestinationResolver
{
    private readonly IGeocodingService _geocoding;

    public DestinationResolver(IGeocodingService geocoding)
    {
        _geocoding = geocoding;
    }

    public async Task<ResolvedDestination> ResolveAsync(
        SearchDestination destination,
        CancellationToken ct)
    {
        if (destination is null)
        {
            throw new DestinationResolveException(
                "DESTINATION_MISSING",
                "Destination must be provided.");
        }

        // 1️⃣ Coordinates win — no external calls
        if (destination.Coordinates is not null)
        {
            return new ResolvedDestination(
                Lat: destination.Coordinates.Lat,
                Lng: destination.Coordinates.Lng,
                Source: DestinationSource.Coordinates,
                NormalisedAddress: null);
        }

        // 2️⃣ Address → geocode
        if (destination.Address is null)
        {
            throw new DestinationResolveException(
                "DESTINATION_INCOMPLETE",
                "Destination must include coordinates or address.");
        }

        var addressText = FormatAddress(destination.Address);

        (double lat, double lng) result;
        try
        {
            result = await _geocoding.GeocodeAsync(addressText, ct);
        }
        catch (Exception ex)
        {
            throw new DestinationResolveException(
                "DESTINATION_GEOCODE_ERROR",
                $"Geocoding provider failed: {ex.Message}");
        }

        // Guard against provider stubs / failures
        if (result.lat == 0d && result.lng == 0d)
        {
            throw new DestinationResolveException(
                "DESTINATION_GEOCODE_FAILED",
                "Destination address could not be resolved.");
        }

        return new ResolvedDestination(
            Lat: result.lat,
            Lng: result.lng,
            Source: DestinationSource.GeocodedAddress,
            NormalisedAddress: addressText);
    }

    private static string FormatAddress(SearchAddress a)
    {
        // Provider-friendly, stable formatting
        // Example: "1 Example Street, Birmingham, B1 1AA, UK"
        var parts = new List<string>
        {
            a.Line1.Trim()
        };

        if (!string.IsNullOrWhiteSpace(a.Line2))
            parts.Add(a.Line2.Trim());

        parts.Add(a.Town.Trim());
        parts.Add(a.Postcode.Trim());
        parts.Add(a.Country.Trim());

        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}



public sealed record ResolvedDestination(
    double Lat,
    double Lng,
    DestinationSource Source,
    string? NormalisedAddress);
