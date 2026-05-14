namespace AFH.Location.Application.Abstractions.Geo;

public interface IRouteCache
{
    bool TryGet(string key, out RouteResult result);
    void Set(string key, RouteResult result, TimeSpan ttl);
}
