using AFH.Location.Application.Abstractions.Geo;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace AFH.Location.Infrastructure.External.Maps.Azure;

public sealed class AzureMapsGeocodingService : IGeocodingService
{
    private static readonly Regex UkPostcodeRegex = new(
        @"\b([A-Z]{1,2}\d[A-Z\d]?\s*\d[A-Z]{2})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        var trimmedAddress = address.Trim();
        var requestedPostcode = TryExtractUkPostcode(trimmedAddress);
        var encoded = UrlEncoder.Default.Encode(trimmedAddress);

        // Azure Maps Search Address (GET)
        // Key constraints:
        // - countrySet=GB forces UK results only
        // - typeahead=false avoids fuzzy autocomplete-like behaviour
        // - idxSet=PAD is intentionally NOT set: PAD restricts results to point addresses
        //   (building-level), which causes postcode queries to match wrong buildings.
        //   Without idxSet, Azure searches all indexes including PostalCodes, returning
        //   accurate centroid coordinates for UK postcodes.
        var url =
            $"https://atlas.microsoft.com/search/address/json" +
            $"?api-version=1.0" +
            $"&query={encoded}" +
            $"&limit=5" +
            $"&countrySet=GB" +
            $"&typeahead=false" +
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

        var result = SelectResult(doc?.Results, requestedPostcode);
        var pos = result?.Position;
        if (pos is null)
            return (0d, 0d);

        // Sanity guard: reject results outside rough UK bounds
        if (!LooksLikeUk(pos.Lat, pos.Lon))
            return (0d, 0d);

        return (pos.Lat, pos.Lon);
    }

    private static AzureMapsSearchResponse.Result? SelectResult(
        IReadOnlyList<AzureMapsSearchResponse.Result>? results,
        string? requestedPostcode)
    {
        if (results is null || results.Count == 0)
            return null;

        if (requestedPostcode is null)
            return results.FirstOrDefault();

        return results.FirstOrDefault(result =>
            string.Equals(
                NormalisePostcode(result.Address?.PostalCode),
                requestedPostcode,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string? TryExtractUkPostcode(string address)
    {
        var match = UkPostcodeRegex.Match(address);
        return match.Success ? NormalisePostcode(match.Groups[1].Value) : null;
    }

    private static string NormalisePostcode(string? postcode)
        => string.Concat(
            (postcode ?? string.Empty)
                .Where(char.IsLetterOrDigit))
            .ToUpperInvariant();

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
            public Address? Address { get; set; }
        }

        public sealed class Position
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
        }

        public sealed class Address
        {
            public string? PostalCode { get; set; }
        }
    }
}
