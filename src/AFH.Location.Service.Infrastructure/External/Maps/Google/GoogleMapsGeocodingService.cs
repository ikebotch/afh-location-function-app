using AFH.Location.Service.Core.Abstractions;

namespace AFH.Location.Service.Infrastructure.External.Maps.Google;

public sealed class GoogleMapsGeocodingService : IGeocodingService
{
    public Task<(double Lat, double Lng)> GeocodeAsync(string address, CancellationToken ct)
        => Task.FromResult((0d, 0d)); // TODO
}
