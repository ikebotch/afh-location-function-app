namespace AFH.Location.Service.Application.Abstractions;

public interface IGeoCache
{
    bool TryGet(string key, out (double Lat, double Lng) coords);
    void Set(string key, (double Lat, double Lng) coords, TimeSpan ttl);
}