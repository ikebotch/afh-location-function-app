using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Domain;
using AFH.Location.Infrastructure.External.Maps;

namespace AFH.Location.Tests;

public sealed class CachedRouteMatrixServiceTests
{
    [Fact]
    public async Task GetOneToManyAsync_UsesSingleRouteCacheHitsBeforeCallingInnerService()
    {
        var inner = new RecordingRouteMatrixService();
        var cache = new StubRouteCache();
        cache.Set("single:51.500000:-0.100000:51.600000:-0.200000", new RouteResult(18, 7.5, "High"), TimeSpan.FromMinutes(30));

        var sut = new CachedRouteMatrixService(inner, cache, new StubRouteMatrixPolicyProvider());

        var result = await sut.GetOneToManyAsync(
            (51.5, -0.1),
            new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
            {
                ["home-1"] = (51.6, -0.2),
                ["home-2"] = (51.7, -0.3)
            },
            ct: CancellationToken.None);

        Assert.Equal(1, inner.OneToManyCallCount);
        Assert.Equal([1], inner.OneToManyBatchSizes);
        Assert.Equal(2, result.Count);
        Assert.Equal(18, result["home-1"].EtaMinutes);
        Assert.Equal(21, result["home-2"].EtaMinutes);
    }

    [Fact]
    public async Task GetOneToManyAsync_PopulatesSingleRouteCacheForLaterSingleRouteLookups()
    {
        var cache = new StubRouteCache();
        var matrixInner = new RecordingRouteMatrixService();
        var routingInner = new RecordingRoutingService();
        var matrix = new CachedRouteMatrixService(matrixInner, cache, new StubRouteMatrixPolicyProvider());
        var routing = new CachedRoutingService(routingInner, cache);

        await matrix.GetOneToManyAsync(
            (51.5, -0.1),
            new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
            {
                ["home-1"] = (51.6, -0.2)
            },
            ct: CancellationToken.None);

        var route = await routing.GetRouteAsync((51.5, -0.1), (51.6, -0.2), CancellationToken.None);

        Assert.Equal(18, route.EtaMinutes);
        Assert.Equal(1, matrixInner.OneToManyCallCount);
        Assert.Equal(0, routingInner.CallCount);
    }

    private sealed class RecordingRouteMatrixService : IRouteMatrixService
    {
        public int OneToManyCallCount { get; private set; }
        public List<int> OneToManyBatchSizes { get; } = [];

        public Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
            IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
            (double Lat, double Lng) destination,
            CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
            (double Lat, double Lng) origin,
            IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
            DateTimeOffset? departAt = null,
            CancellationToken ct = default)
        {
            OneToManyCallCount++;
            OneToManyBatchSizes.Add(destinations.Count);

            IReadOnlyDictionary<string, RouteResult> result = destinations.ToDictionary(
                x => x.Key,
                x => x.Key.Equals("home-2", StringComparison.OrdinalIgnoreCase)
                    ? new RouteResult(21, 9.0, "High")
                    : new RouteResult(18, 7.5, "High"),
                StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(result);
        }
    }

    private sealed class StubRouteMatrixPolicyProvider : IRouteMatrixPolicyProvider
    {
        public Task<RouteMatrixPolicy> GetAsync(CancellationToken ct)
            => Task.FromResult(new RouteMatrixPolicy());
    }

    private sealed class RecordingRoutingService : IRoutingService
    {
        public int CallCount { get; private set; }

        public Task<RouteResult> GetRouteAsync(
            (double Lat, double Lng) origin,
            (double Lat, double Lng) destination,
            CancellationToken ct)
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
