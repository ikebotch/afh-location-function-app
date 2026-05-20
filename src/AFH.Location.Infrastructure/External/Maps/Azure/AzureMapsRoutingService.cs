using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Domain.Travel;
using Microsoft.Extensions.Configuration;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AFH.Location.Infrastructure.External.Maps.Azure;

public sealed class AzureMapsRoutingService : IRoutingService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public AzureMapsRoutingService(IHttpClientFactory httpFactory, IConfiguration cfg)
    {
        _http = httpFactory.CreateClient(nameof(AzureMapsRoutingService));
        _cfg = cfg;
    }

    public async Task<RouteResult> GetRouteAsync(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        CancellationToken ct)
    {
        var key = _cfg["Maps:Azure:Key"] ?? _cfg["Maps:Azure:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Maps:Azure:Key is missing.");

        var query = $"{origin.Lat},{origin.Lng}:{destination.Lat},{destination.Lng}";
        var encodedQuery = UrlEncoder.Default.Encode(query);

        var url =
            $"https://atlas.microsoft.com/route/directions/json" +
            $"?api-version=1.0" +
            $"&query={encodedQuery}" +
            $"&travelMode=car" +
            $"&routeType=fastest" +
            $"&subscription-key={key}";

        using var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"AzureMaps route failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

        var doc = JsonSerializer.Deserialize<RouteDirectionsResponse>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var summary = doc?.Routes?.FirstOrDefault()?.Summary;
        if (summary is null)
            throw new InvalidOperationException(
                $"AzureMaps route returned no summary. Body: {body}");

        var miles = summary.LengthInMeters / 1609.344d;
        var minutes = (int)Math.Ceiling(summary.TravelTimeInSeconds / 60d);

        return new RouteResult(minutes, Math.Round(miles, 2), "High", TravelRouteResolutionSource.AzureMaps);
    }

    private sealed class RouteDirectionsResponse
    {
        public List<Route>? Routes { get; set; }

        public sealed class Route
        {
            public Summary? Summary { get; set; }
        }

        public sealed class Summary
        {
            public double LengthInMeters { get; set; }
            public int TravelTimeInSeconds { get; set; }
        }
    }
}
