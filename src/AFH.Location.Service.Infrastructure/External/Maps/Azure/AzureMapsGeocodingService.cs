using AFH.Location.Service.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Encodings.Web;

namespace AFH.Location.Service.Infrastructure.External.Maps.Azure;

public sealed class AzureMapsGeocodingService : IGeocodingService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public AzureMapsGeocodingService(IHttpClientFactory httpFactory, IConfiguration cfg)
    {
        _http = httpFactory.CreateClient(nameof(AzureMapsGeocodingService));
        _cfg = cfg;
    }

    public async Task<(double Lat, double Lng)> GeocodeAsync(string address, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(address))
            return (0d, 0d);

        var key = _cfg["Maps:Azure:Key"] ?? _cfg["Maps:Azure:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Maps:Azure:Key is missing.");

        var encoded = UrlEncoder.Default.Encode(address.Trim());

        // Azure Maps Search Address (GET)
        // Key constraints:
        // - countrySet=GB forces UK
        // - idxSet=PAD biases towards postal address / postcode results
        // - typeahead=false avoids fuzzy autocomplete-like behaviour
        var url =
            $"https://atlas.microsoft.com/search/address/json" +
            $"?api-version=1.0" +
            $"&query={encoded}" +
            $"&limit=1" +
            $"&countrySet=GB" +
            $"&typeahead=false" +
            $"&idxSet=PAD" +
            $"&subscription-key={key}";

        AzureMapsSearchResponse? doc;
        try
        {
            doc = await _http.GetFromJsonAsync<AzureMapsSearchResponse>(url, ct);
        }
        catch
        {
            // Treat as non-resolvable (caller will apply FailureTtl caching)
            return (0d, 0d);
        }

        var pos = doc?.Results?.FirstOrDefault()?.Position;
        if (pos is null) return (0d, 0d);

        // Sanity guard: reject results outside rough UK bounds
        if (!LooksLikeUk(pos.Lat, pos.Lon))
            return (0d, 0d);

        return (pos.Lat, pos.Lon);
    }

    private static bool LooksLikeUk(double lat, double lon)
    {
        // Rough bounding box for UK (prevents totally wrong hits)
        // Lat: ~49 to 61, Lon: ~-8.5 to 2.5
        return lat is >= 49.0 and <= 61.0
            && lon is >= -8.5 and <= 2.5;
    }

    private sealed class AzureMapsSearchResponse
    {
        public List<Result>? Results { get; set; }

        public sealed class Result
        {
            public Position? Position { get; set; }
        }

        public sealed class Position
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
        }
    }
}