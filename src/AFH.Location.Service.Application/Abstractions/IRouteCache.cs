namespace AFH.Location.Service.Application.Abstractions;

public interface IRouteCache
{
    bool TryGet(string key, out RouteResult result);
    void Set(string key, RouteResult result, TimeSpan ttl);
}
