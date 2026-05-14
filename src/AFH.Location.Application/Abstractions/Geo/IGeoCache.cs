namespace AFH.Location.Application.Abstractions.Geo;

public interface IGeoCache
{
    bool TryGet(string key, out (double Lat, double Lng) coords);
    void Set(string key, (double Lat, double Lng) coords, TimeSpan ttl);
}