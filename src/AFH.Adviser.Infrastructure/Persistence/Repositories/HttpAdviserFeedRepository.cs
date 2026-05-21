using AFH.Adviser.Application.Abstractions;
using Entities = AFH.Adviser.Domain.Entities;
using AFH.Adviser.Application.Advisers;
using AFH.Adviser.Domain.Entities;
using AFH.Adviser.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFH.Adviser.Infrastructure.Persistence.Repositories;

public sealed class HttpAdviserFeedRepository : IAdviserSourceRepository
{
    private readonly HttpClient _http;
    private readonly AdviserFeedOptions _options;
    private readonly ILogger<HttpAdviserFeedRepository> _logger;

    public HttpAdviserFeedRepository(
        IHttpClientFactory httpFactory,
        IOptions<AdviserFeedOptions> options,
        ILogger<HttpAdviserFeedRepository> logger)
    {
        _http = httpFactory.CreateClient(nameof(HttpAdviserFeedRepository));
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(
        IReadOnlyCollection<string>? adviserIds,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException("AdviserFeed:BaseUrl is required when AdviserFeed:Enabled=true.");

        var ids = adviserIds?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        var url = BuildUrl(ids);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            request.Headers.TryAddWithoutValidation("x-functions-key", _options.ApiKey);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Entities.Adviser feed call failed with status {StatusCode}.", (int)response.StatusCode);
            return Array.Empty<Entities.Adviser>();
        }

        var payload = await ReadPayloadAsync(response, ct);
        if (payload.Count == 0)
            return Array.Empty<Entities.Adviser>();

        return payload
            .Where(x => !string.IsNullOrWhiteSpace(x.AdviserId))
            .Select(x => new Entities.Adviser
            {
                AdviserId = x.AdviserId.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? x.AdviserId.Trim() : x.DisplayName.Trim(),
                MailboxUserId = string.IsNullOrWhiteSpace(x.MailboxUserId) ? x.AdviserId.Trim() : x.MailboxUserId.Trim(),
                HomePostcode = x.HomePostcode?.Trim() ?? string.Empty,
                Region = x.Region?.Trim() ?? string.Empty,
                Skills = SkillNormaliser.NormaliseSkills(x.Skills),
                Rating = x.Rating,
                IsActive = x.IsActive,
                IsBookable = x.IsBookable,
                CoverageRadiusMiles = x.CoverageRadiusMiles is > 0 ? x.CoverageRadiusMiles : null,
                MaxTravelTimeMinutes = x.MaxTravelTimeMinutes is > 0 ? x.MaxTravelTimeMinutes : null,
                LastSyncedUtc = DateTime.UtcNow
            })
            .ToList();
    }

    private string BuildUrl(IReadOnlyList<string> ids)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var path = _options.EndpointPath.StartsWith('/')
            ? _options.EndpointPath
            : "/" + _options.EndpointPath;

        if (ids.Count == 0)
            return baseUrl + path;

        var joinedIds = string.Join(",", ids.Select(Uri.EscapeDataString));
        var separator = path.Contains('?') ? "&" : "?";
        return $"{baseUrl}{path}{separator}ids={joinedIds}";
    }

    private static async Task<List<AdviserDto>> ReadPayloadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var envelope = JsonSerializer.Deserialize<AdviserEnvelope>(json, options);
            if (envelope?.Data?.Advisers is { Count: > 0 })
                return envelope.Data.Advisers;

            var direct = JsonSerializer.Deserialize<List<AdviserDto>>(json, options);
            return direct ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed class AdviserEnvelope
    {
        public AdviserEnvelopeData? Data { get; set; }
    }

    private sealed class AdviserEnvelopeData
    {
        public List<AdviserDto> Advisers { get; set; } = [];
    }

    private sealed class AdviserDto
    {
        public string AdviserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string HomePostcode { get; set; } = string.Empty;
        public string MailboxUserId { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public List<string>? Skills { get; set; }
        public double Rating { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsBookable { get; set; } = true;
        public double? CoverageRadiusMiles { get; set; }
        public int? MaxTravelTimeMinutes { get; set; }
    }
}
