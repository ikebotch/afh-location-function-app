namespace AFH.Location.Service.Core.Abstractions;

public interface IGeocodingService
{
    Task<(double Lat, double Lng)> GeocodeAsync(string address, CancellationToken ct);
}
