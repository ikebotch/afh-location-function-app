using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Application.Services.Common;
using AFH.Location.Service.Application.Models.V1;
using AFH.Location.Service.Infrastructure.External.Maps.Google;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Service.UnitTests;

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

    [Fact]
    public void AvailabilityEvaluator_UsesConfiguredBusinessTimezone()
    {
        var sut = new AvailabilityEvaluator(new StubTimeZoneProvider("UTC"));

        var meeting = new LocationMeetingWindow
        {
            RequestedStartUtc = new DateTime(2026, 03, 25, 7, 5, 0, DateTimeKind.Utc),
            DurationMinutes = 60,
            SearchHorizonMinutes = 180
        };

        var result = sut.Evaluate(meeting, []);

        Assert.Equal("Available", result.Status);
        Assert.Equal(new DateTime(2026, 03, 25, 8, 0, 0, DateTimeKind.Utc), result.ProposedStartUtc);
    }

    private sealed class StubTimeZoneProvider : IBusinessTimeZoneProvider
    {
        public StubTimeZoneProvider(string timeZoneId) => TimeZoneId = timeZoneId;
        public string TimeZoneId { get; }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
