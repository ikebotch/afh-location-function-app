using AFH.Location.Application.Abstractions;
using AFH.Location.Infrastructure.External.Maps;

namespace AFH.Location.Tests;

public class CachedRoutingServiceTests
{
    [Fact]
    public async Task GetRouteAsync_UsesCacheAfterFirstLookup()
    {
        var inner = new StubRoutingService();
        var cache = new StubRouteCache();
        var sut = new CachedRoutingService(inner, cache);

        var first = await sut.GetRouteAsync((51.5, -0.1), (51.6, -0.2), CancellationToken.None);
        var second = await sut.GetRouteAsync((51.5, -0.1), (51.6, -0.2), CancellationToken.None);

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(first.EtaMinutes, second.EtaMinutes);
    }

    private sealed class StubRoutingService : IRoutingService
    {
        public int CallCount { get; private set; }

        public Task<RouteResult> GetRouteAsync((double Lat, double Lng) origin, (double Lat, double Lng) destination, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new RouteResult(25, 12.5, "High"));
        }
    }

    private sealed class StubRouteCache : IRouteCache
    {
        private readonly Dictionary<string, RouteResult> _items = new(StringComparer.OrdinalIgnoreCase);

        public bool TryGet(string key, out RouteResult result) => _items.TryGetValue(key, out result!);

        public void Set(string key, RouteResult result, TimeSpan ttl) => _items[key] = result;
    }
}
