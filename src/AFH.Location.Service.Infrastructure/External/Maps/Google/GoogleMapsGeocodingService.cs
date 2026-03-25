using AFH.Location.Service.Core.Abstractions;

namespace AFH.Location.Service.Infrastructure.External.Maps.Google;

public sealed class GoogleMapsGeocodingService : IGeocodingService
{
    public Task<(double Lat, double Lng)> GeocodeAsync(string address, CancellationToken ct)
        => throw new NotSupportedException("Google geocoding is disabled because the provider path is incomplete and must not return fake coordinates.");
}
