using AFH.Location.Application.Models.V1;
using AFH.Location.Infrastructure.External.Calendar;
using AFH.Location.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace AFH.Location.Tests;

public class CalendarServiceClientTests
{
    [Fact]
    public async Task GetAdviserAvailabilityBatchAsync_UsesFunctionKeyAndBearerAuth()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"schedules\":[{\"userId\":\"ADV-1\",\"state\":\"Ok\",\"message\":null,\"bookings\":[]}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var sut = new CalendarServiceClient(
            new StubHttpClientFactory(new HttpClient(handler)),
            Options.Create(new CalendarServiceOptions
            {
                BaseUrl = "https://calendar.example",
                FunctionKey = "calendar-function-key",
                InternalToken = "calendar-internal-token"
            }),
            NullLogger<CalendarServiceClient>.Instance);

        await sut.GetAdviserAvailabilityBatchAsync(
            ["ADV-1"],
            new LocationMeetingWindow
            {
                RequestedStartUtc = new DateTime(2026, 03, 25, 10, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60,
                SearchHorizonMinutes = 120
            },
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("x-functions-key", out var functionKeyValues));
        Assert.Equal("calendar-function-key", functionKeyValues!.Single());
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("calendar-internal-token", captured.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task EnsureSubscriptionsAsync_UsesEnsureEndpointAndAuth()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"requestedCount\":2,\"processedCount\":2,\"createdCount\":1,\"renewedCount\":0,\"alreadyActiveCount\":1,\"invalidUserCount\":0,\"unavailableMailboxCount\":0,\"failedCount\":0,\"results\":[]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var sut = new CalendarServiceClient(
            new StubHttpClientFactory(new HttpClient(handler)),
            Options.Create(new CalendarServiceOptions
            {
                BaseUrl = "https://calendar.example",
                FunctionKey = "calendar-function-key",
                InternalToken = "calendar-internal-token"
            }),
            NullLogger<CalendarServiceClient>.Instance);

        var result = await sut.EnsureSubscriptionsAsync(["a@tenant.com", "b@tenant.com"], CancellationToken.None);

        Assert.Equal(2, result.RequestedCount);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://calendar.example/api/v1/calendar/subscriptions/ensure", captured.RequestUri?.ToString());
        Assert.True(captured.Headers.TryGetValues("x-functions-key", out var functionKeyValues));
        Assert.Equal("calendar-function-key", functionKeyValues!.Single());
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("calendar-internal-token", captured.Headers.Authorization?.Parameter);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handle;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle)
        {
            _handle = handle;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handle(request));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }
}
