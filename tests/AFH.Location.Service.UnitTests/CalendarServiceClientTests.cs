using AFH.Location.Service.Application.Models.V1;
using AFH.Location.Service.Infrastructure.External.Calendar;
using AFH.Location.Service.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace AFH.Location.Service.UnitTests;

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
