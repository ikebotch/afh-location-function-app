using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Contracts.V1.Requests;
using AFH.Location.Service.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;

namespace AFH.Location.Service.Infrastructure.External.Calendar;

public sealed class CalendarServiceClient : ICalendarServiceClient
{
    private readonly HttpClient _http;
    private readonly CalendarServiceOptions _options;
    private readonly ILogger<CalendarServiceClient> _logger;

    public CalendarServiceClient(
        IHttpClientFactory httpFactory,
        IOptions<CalendarServiceOptions> options,
        ILogger<CalendarServiceClient> logger)
    {
        _http = httpFactory.CreateClient(nameof(CalendarServiceClient));
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AdviserAvailability> GetAdviserAvailabilityAsync(
        string adviserId,
        MeetingWindow window,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogWarning("CalendarService:BaseUrl is missing; returning empty availability.");
            return NewEmptyAvailability(adviserId);
        }

        var startUtc = window.RequestedStartUtc;
        var endUtc = window.RequestedStartUtc.AddMinutes(Math.Max(1, window.SearchHorizonMinutes + window.DurationMinutes));

        var url =
            $"{_options.BaseUrl.TrimEnd('/')}/api/v1/calendar/users/{Uri.EscapeDataString(adviserId)}/schedule" +
            $"?startUtc={Uri.EscapeDataString(startUtc.ToString("O"))}" +
            $"&endUtc={Uri.EscapeDataString(endUtc.ToString("O"))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuth(request);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return NewEmptyAvailability(adviserId);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Calendar schedule call failed for AdviserId={AdviserId}. Status={StatusCode}",
                    adviserId,
                    (int)response.StatusCode);
                return NewEmptyAvailability(adviserId);
            }

            var dto = await response.Content.ReadFromJsonAsync<ScheduleResponse>(cancellationToken: ct);
            if (dto?.Bookings is null || dto.Bookings.Count == 0)
                return NewEmptyAvailability(adviserId);

            var busy = dto.Bookings
                .Where(b => !string.Equals(b.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .Select(b => new BusyBlock
                {
                    StartUtc = b.StartUtc,
                    EndUtc = b.EndUtc
                })
                .ToList();

            return new AdviserAvailability
            {
                AdviserId = adviserId,
                BusyBlocks = busy,
                IsOutOfOffice = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Calendar schedule call threw for AdviserId={AdviserId}", adviserId);
            return NewEmptyAvailability(adviserId);
        }
    }

    public async Task<CalendarAppointmentResult> CreateAppointmentAsync(
        CreateCalendarAppointmentRequest request,
        CancellationToken ct)
    {
        EnsureBaseUrl();
        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/v1/calendar/appointments";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };
        AddAuth(httpRequest);

        using var response = await _http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<CalendarAppointmentResult>(cancellationToken: ct);
        return body ?? new CalendarAppointmentResult();
    }

    public async Task UpdateAppointmentAsync(
        UpdateCalendarAppointmentRequest request,
        CancellationToken ct)
    {
        EnsureBaseUrl();
        if (string.IsNullOrWhiteSpace(request.AppointmentId))
            throw new ArgumentException("AppointmentId is required.", nameof(request));

        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/v1/calendar/appointments/{Uri.EscapeDataString(request.AppointmentId)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(request)
        };
        AddAuth(httpRequest);

        using var response = await _http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelAppointmentAsync(
        CancelCalendarAppointmentRequest request,
        CancellationToken ct)
    {
        EnsureBaseUrl();
        if (string.IsNullOrWhiteSpace(request.AppointmentId))
            throw new ArgumentException("AppointmentId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.UserId))
            throw new ArgumentException("UserId is required.", nameof(request));

        var url =
            $"{_options.BaseUrl.TrimEnd('/')}/api/v1/calendar/appointments/{Uri.EscapeDataString(request.AppointmentId)}" +
            $"?userId={Uri.EscapeDataString(request.UserId)}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, url);
        AddAuth(httpRequest);

        using var response = await _http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
    }

    private void EnsureBaseUrl()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException("CalendarService:BaseUrl is required.");
    }

    private void AddAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
            request.Headers.Add("x-functions-key", _options.FunctionKey);
    }

    private static AdviserAvailability NewEmptyAvailability(string adviserId) => new()
    {
        AdviserId = adviserId,
        BusyBlocks = Array.Empty<BusyBlock>(),
        IsOutOfOffice = false
    };

    private sealed class ScheduleResponse
    {
        public List<BookingSummary> Bookings { get; set; } = new();
    }

    private sealed class BookingSummary
    {
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
