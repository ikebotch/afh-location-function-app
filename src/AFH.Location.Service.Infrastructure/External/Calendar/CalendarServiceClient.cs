using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Contracts.V1.Requests;
using AFH.Location.Service.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            _logger.LogWarning("CalendarService:BaseUrl is missing; returning unavailable calendar state.");
            return NewAvailability(adviserId, CalendarAvailabilityState.ConfigurationMissing, "CalendarService:BaseUrl is missing.");
        }

        var startUtc = window.RequestedStartUtc.AddMinutes(-Math.Max(0, _options.ScheduleLookbackMinutes));
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
            {
                _logger.LogWarning("Calendar mailbox/schedule not found for AdviserId={AdviserId}", adviserId);
                return NewAvailability(adviserId, CalendarAvailabilityState.MailboxNotFound, "Mailbox or schedule not found.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Calendar schedule call failed for AdviserId={AdviserId}. Status={StatusCode}",
                    adviserId,
                    (int)response.StatusCode);
                return NewAvailability(
                    adviserId,
                    CalendarAvailabilityState.ServiceUnavailable,
                    $"Calendar service HTTP {(int)response.StatusCode}");
            }

            var schedule = await ReadEnvelopedOrRawAsync<ScheduleResponse>(response, ct);

            if (schedule is null)
            {
                _logger.LogWarning("Calendar schedule payload parse failed for AdviserId={AdviserId}", adviserId);
                return NewAvailability(
                    adviserId,
                    CalendarAvailabilityState.ServiceUnavailable,
                    "Calendar schedule payload parse failed.");
            }

            if (schedule.Bookings.Count == 0)
                return NewAvailability(adviserId, CalendarAvailabilityState.Ok, null);

            var bookings = schedule.Bookings
                .Where(IsBlockingBooking)
                .OrderBy(b => b.StartUtc)
                .ToList();

            var busy = bookings
                .Select(b => new BusyBlock
                {
                    StartUtc = b.StartUtc,
                    EndUtc = b.EndUtc,
                    LocationPostcode = b.ResolvePostcode()
                })
                .ToList();

            var previous = bookings
                .Where(b => b.EndUtc <= window.RequestedStartUtc)
                .OrderByDescending(b => b.EndUtc)
                .FirstOrDefault();

            var current = bookings
                .FirstOrDefault(b => b.StartUtc <= window.RequestedStartUtc && b.EndUtc >= window.RequestedStartUtc);

            var currentLocationPostcode = current?.ResolvePostcode() ?? previous?.ResolvePostcode();

            return new AdviserAvailability
            {
                AdviserId = adviserId,
                BusyBlocks = busy,
                IsOutOfOffice = false,
                CurrentLocationPostcode = currentLocationPostcode,
                State = CalendarAvailabilityState.Ok
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Calendar schedule call threw for AdviserId={AdviserId}", adviserId);
            return NewAvailability(
                adviserId,
                CalendarAvailabilityState.UnknownError,
                "Calendar schedule call threw an exception.");
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

        var body = await ReadEnvelopedOrRawAsync<CalendarAppointmentResult>(response, ct);
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

    private static AdviserAvailability NewAvailability(
        string adviserId,
        CalendarAvailabilityState state,
        string? message) => new()
        {
            AdviserId = adviserId,
            BusyBlocks = Array.Empty<BusyBlock>(),
            IsOutOfOffice = false,
            State = state,
            StateMessage = message
        };

    private static async Task<T?> ReadEnvelopedOrRawAsync<T>(
        HttpResponseMessage response,
        CancellationToken ct)
        where T : class
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            var enveloped = JsonSerializer.Deserialize<ApiEnvelope<T>>(json);
            if (enveloped?.Data is not null)
                return enveloped.Data;

            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    private sealed class ScheduleResponse
    {
        public List<BookingSummary> Bookings { get; set; } = new();
    }

    private sealed class ApiEnvelope<T> where T : class
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }

    private sealed class BookingSummary
    {
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Postcode { get; set; }
        public string? LocationPostcode { get; set; }
        public string? ClientPostcode { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }

        public string? ResolvePostcode()
        {
            if (!string.IsNullOrWhiteSpace(LocationPostcode)) return LocationPostcode;
            if (!string.IsNullOrWhiteSpace(Postcode)) return Postcode;
            if (!string.IsNullOrWhiteSpace(ClientPostcode)) return ClientPostcode;
            if (ExtensionData is null || ExtensionData.Count == 0) return null;

            if (TryReadString(ExtensionData, "postcode", out var p)) return p;
            if (TryReadString(ExtensionData, "locationPostcode", out p)) return p;
            if (TryReadString(ExtensionData, "clientPostcode", out p)) return p;
            if (TryReadNestedPostcode(ExtensionData, "location", out p)) return p;
            if (TryReadNestedPostcode(ExtensionData, "address", out p)) return p;

            return null;
        }

        private static bool TryReadString(
            IReadOnlyDictionary<string, JsonElement> data,
            string key,
            out string? value)
        {
            value = null;
            if (!data.TryGetValue(key, out var element)) return false;
            if (element.ValueKind != JsonValueKind.String) return false;
            var candidate = element.GetString();
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            value = candidate;
            return true;
        }

        private static bool TryReadNestedPostcode(
            IReadOnlyDictionary<string, JsonElement> data,
            string key,
            out string? value)
        {
            value = null;
            if (!data.TryGetValue(key, out var element)) return false;
            if (element.ValueKind != JsonValueKind.Object) return false;
            if (!element.TryGetProperty("postcode", out var postcodeElement)) return false;
            if (postcodeElement.ValueKind != JsonValueKind.String) return false;
            var candidate = postcodeElement.GetString();
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            value = candidate;
            return true;
        }
    }

    private static bool IsBlockingBooking(BookingSummary booking)
    {
        if (string.Equals(booking.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return false;

        var normalized = (booking.Status ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "busy" or "tentative" or "oof" or "outofoffice" or "workingelsewhere";
    }
}
