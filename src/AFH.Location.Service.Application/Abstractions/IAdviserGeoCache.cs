namespace AFH.Location.Service.Application.Abstractions;

public interface IAdviserGeoCache
{
    bool TryGet(string key, out (double Lat, double Lng) coords);
    void Set(string key, (double Lat, double Lng) coords, TimeSpan ttl);
}