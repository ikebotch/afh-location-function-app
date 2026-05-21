using AFH.Adviser.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFH.Adviser.Application.Abstractions.Clients;

namespace AFH.Adviser.Infrastructure.External.Calendar;

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

    public async Task<CalendarSubscriptionEnsureSummary> EnsureSubscriptionsAsync(
        IReadOnlyList<string> userIds,
        CancellationToken ct)
    {
        if (userIds is null || userIds.Count == 0)
            return new CalendarSubscriptionEnsureSummary();

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogWarning("CalendarService:BaseUrl is missing; skipping subscription ensure.");
            return new CalendarSubscriptionEnsureSummary();
        }

        var ids = userIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.Length == 0)
            return new CalendarSubscriptionEnsureSummary();

        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/v1/calendar/subscriptions/ensure";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                userIds = ids
            })
        };
        AddAuth(request);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await ReadEnvelopedOrRawAsync<CalendarSubscriptionEnsureSummary>(response, ct)
            ?? new CalendarSubscriptionEnsureSummary();
    }

    private void AddAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
            request.Headers.TryAddWithoutValidation("x-functions-key", _options.FunctionKey.Trim());

        if (!string.IsNullOrWhiteSpace(_options.InternalToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.InternalToken.Trim());
    }

    private async Task<T?> ReadEnvelopedOrRawAsync<T>(
       HttpResponseMessage response,
       CancellationToken ct)
       where T : class
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            return default;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            var enveloped = JsonSerializer.Deserialize<ApiEnvelope<T>>(json, options);
            if (enveloped?.Data is not null)
                return enveloped.Data;

            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Calendar service returned malformed JSON while ensuring adviser subscriptions. Status={StatusCode}",
                (int)response.StatusCode);

            throw new InvalidOperationException("Calendar service returned malformed JSON.", ex);
        }
    }

    private sealed class ApiEnvelope<T> where T : class
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }
}
