using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Models.V1.Requests;
using AFH.Location.Application.Services.Common;
using AFH.Location.Domain;

namespace AFH.Location.Tests;

public sealed class DestinationCoordinateResolverTests
{
    [Fact]
    public async Task ResolveAsync_UsesPostcodeCacheForRepeatedDestinationGeocodes()
    {
        var geocoding = new RecordingGeocodingService();
        var cache = new StubGeoCache();
        var sut = new DestinationCoordinateResolver(
            cache,
            geocoding,
            new StubGeoCachePolicyProvider());

        var destination = new SearchDestination
        {
            Address = new SearchAddress
            {
                Line1 = "1 High Street",
                Town = "London",
                Postcode = "SW1A 1AA",
                Country = "UK"
            }
        };

        var first = await sut.ResolveAsync(destination, CancellationToken.None);
        var second = await sut.ResolveAsync(destination, CancellationToken.None);

        Assert.Equal(1, geocoding.CallCount);
        Assert.Equal(first.Lat, second.Lat);
        Assert.Equal(first.Lng, second.Lng);
    }

    private sealed class RecordingGeocodingService : IGeocodingService
    {
        public int CallCount { get; private set; }

        public Task<(double Lat, double Lng)> GeocodeAsync(string address, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult((51.501, -0.141));
        }
    }

    private sealed class StubGeoCache : IGeoCache
    {
        private readonly Dictionary<string, (double Lat, double Lng)> _items = new(StringComparer.OrdinalIgnoreCase);

        public bool TryGet(string key, out (double Lat, double Lng) coords) => _items.TryGetValue(key, out coords);

        public void Set(string key, (double Lat, double Lng) coords, TimeSpan ttl) => _items[key] = coords;
    }

    private sealed class StubGeoCachePolicyProvider : IGeoCachePolicyProvider
    {
        public Task<GeoCachePolicy> GetAsync(CancellationToken ct) => Task.FromResult(new GeoCachePolicy
        {
            AdviserHomeTtl = TimeSpan.FromDays(30),
            AdviserOfficeTtl = TimeSpan.FromDays(30),
            DestinationTtl = TimeSpan.FromDays(1),
            FailureTtl = TimeSpan.FromMinutes(5),
            SuccessTtl = TimeSpan.FromDays(1)
        });
    }
}