using AFH.Location.Infrastructure.External.Maps.Google;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Tests;

public class ProviderSafetyTests
{
    [Fact]
    public async Task GoogleMapsGeocodingService_FailsFast_InsteadOfReturningZeroCoordinates()
    {
        var sut = new GoogleMapsGeocodingService();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => sut.GeocodeAsync("1 Example St", CancellationToken.None));

        Assert.Contains("fake coordinates", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GoogleMapsRoutingService_FailsFast_InsteadOfReturningFakeRoute()
    {
        var sut = new GoogleMapsRoutingService(new StubHttpClientFactory(), new ConfigurationBuilder().Build());

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.GetRouteAsync((51.5, -0.1), (51.6, -0.2), CancellationToken.None));

        Assert.Contains("fake routing data", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
