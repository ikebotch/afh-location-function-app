namespace AFH.Location.Service.Application.Abstractions;

public interface IGeocodingService
{
    Task<(double Lat, double Lng)> GeocodeAsync(string address, CancellationToken ct);
}
