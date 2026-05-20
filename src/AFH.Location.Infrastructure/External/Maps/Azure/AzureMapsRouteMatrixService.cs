using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Domain.Travel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AFH.Location.Infrastructure.External.Maps.Azure;

public sealed class AzureMapsRouteMatrixService : IRouteMatrixService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AzureMapsRouteMatrixService>? _logger;

    public AzureMapsRouteMatrixService(
        IHttpClientFactory httpFactory,
        IConfiguration cfg,
        ILogger<AzureMapsRouteMatrixService>? logger = null)
    {
        _http = httpFactory.CreateClient(nameof(AzureMapsRouteMatrixService));
        _cfg = cfg;
        _logger = logger;
    }

    /// <summary>
    /// Many origins (advisers) -> one destination (client). Your existing method may already do this.
    /// Keep it if you already have it; otherwise you can map it to the generic helper below.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
        IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
        (double Lat, double Lng) destination,
        CancellationToken ct)
    {
        if (adviserOrigins.Count == 0)
            return new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);

        // Build: origins = advisers, destinations = single client destination (keyed by "DEST")
        var origins = adviserOrigins;
        var destinations = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
        {
            ["DEST"] = destination
        };

        Dictionary<(string OriginId, string DestId), RouteResult> matrix;
        try
        {
            matrix = await ExecuteMatrixAsync(origins, destinations, null, ct);
        }
        catch (TimeoutException ex)
        {
            _logger?.LogWarning(
                ex,
                "Azure Maps route matrix timed out. OriginCount={OriginCount} DestinationCount={DestinationCount}",
                origins.Count,
                destinations.Count);
            matrix = [];
        }

        // Extract each adviser -> DEST cell
        var results = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var adviserId in adviserOrigins.Keys)
        {
            if (matrix.TryGetValue((adviserId, "DEST"), out var rr))
                results[adviserId] = rr;
            else
                results[adviserId] = new RouteResult(0, 0, "Low", TravelRouteResolutionSource.AzureMaps);
        }

        return results;
    }

    /// <summary>
    /// One origin -> many destinations (used for TravelToBase and nearest office).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
        (double Lat, double Lng) origin,
        IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
        DateTimeOffset? departAt = null,
        CancellationToken ct = default)
    {
        if (destinations.Count == 0)
            return new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);

        var origins = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ORIGIN"] = origin
        };

        Dictionary<(string OriginId, string DestId), RouteResult> matrix;
        try
        {
            matrix = await ExecuteMatrixAsync(origins, destinations, departAt, ct);
        }
        catch (TimeoutException ex)
        {
            _logger?.LogWarning(
                ex,
                "Azure Maps route matrix timed out. OriginCount={OriginCount} DestinationCount={DestinationCount}",
                origins.Count,
                destinations.Count);
            matrix = [];
        }

        // Extract ORIGIN -> each destination id
        var results = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var destId in destinations.Keys)
        {
            if (matrix.TryGetValue(("ORIGIN", destId), out var rr))
                results[destId] = rr;
            else
                results[destId] = new RouteResult(0, 0, "Low", TravelRouteResolutionSource.AzureMaps);
        }

        return results;
    }

    // ---------------------------------------------------------------------
    // Generic matrix execution: many origins -> many destinations
    // Returns a map keyed by (originId, destId)
    // ---------------------------------------------------------------------
    private async Task<Dictionary<(string OriginId, string DestId), RouteResult>> ExecuteMatrixAsync(
        IReadOnlyDictionary<string, (double Lat, double Lng)> origins,
        IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
        DateTimeOffset? departAt,
        CancellationToken ct)
    {
        var key = _cfg["Maps:Azure:Key"] ?? _cfg["Maps:Azure:ApiKey"]; // support either name
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Maps:Azure:Key (or Maps:Azure:ApiKey) is missing.");

        // Azure expects coordinates as [lon, lat]
        var originIds = origins.Keys.ToList();
        var destIds = destinations.Keys.ToList();

        var payload = new
        {
            origins = new
            {
                type = "MultiPoint",
                coordinates = originIds
                    .Select(id => new[] { origins[id].Lng, origins[id].Lat })
                    .ToArray()
            },
            destinations = new
            {
                type = "MultiPoint",
                coordinates = destIds
                    .Select(id => new[] { destinations[id].Lng, destinations[id].Lat })
                    .ToArray()
            }
        };

        // POST matrix (async)
        var url =
            $"https://atlas.microsoft.com/route/matrix/json" +
            $"?api-version=1.0" +
            $"&travelMode=car" +
            $"&routeType=fastest" +
            $"&subscription-key={UrlEncoder.Default.Encode(key)}";

        if (departAt.HasValue)
        {
            url += $"&departAt={UrlEncoder.Default.Encode(departAt.Value.ToString("o"))}";
        }

        using var msg = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(msg, ct);

        // Azure matrix is often 202 Accepted and returns Operation-Location or Location header
        if (resp.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            var pollUrl =
                resp.Headers.TryGetValues("Operation-Location", out var opLoc) ? opLoc.FirstOrDefault()
                : resp.Headers.TryGetValues("Location", out var loc) ? loc.FirstOrDefault()
                : null;

            if (string.IsNullOrWhiteSpace(pollUrl))
                throw new InvalidOperationException("AzureMaps matrix returned 202 but no Operation-Location/Location header.");

            // IMPORTANT: poll URL must include key. If it doesn’t, append it.
            pollUrl = EnsureSubscriptionKey(pollUrl!, key);

            var body = await PollForMatrixResultAsync(pollUrl, ct);
            return ParseMatrix(body, originIds, destIds);
        }

        // Sometimes Azure can return 200 with body immediately (rare). Handle it.
        if (resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
                throw new InvalidOperationException("AzureMaps matrix returned success but empty body.");

            return ParseMatrix(body, originIds, destIds);
        }

        var errorBody = await resp.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"AzureMaps matrix failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {errorBody}");
    }

    private static string EnsureSubscriptionKey(string pollUrl, string key)
    {
        // If already contains subscription-key, leave it.
        if (pollUrl.Contains("subscription-key=", StringComparison.OrdinalIgnoreCase))
            return pollUrl;

        var joiner = pollUrl.Contains('?') ? "&" : "?";
        return pollUrl + joiner + "subscription-key=" + UrlEncoder.Default.Encode(key);
    }

    private async Task<string> PollForMatrixResultAsync(string pollUrl, CancellationToken ct)
    {
        var maxAttempts = GetConfiguredPositiveInt("Maps:Azure:MatrixMaxPollAttempts", 10);
        var delayMs = GetConfiguredPositiveInt("Maps:Azure:MatrixPollDelayMs", 400);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var pollResp = await _http.GetAsync(pollUrl, ct);

            if (pollResp.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct);
                continue;
            }

            if (!pollResp.IsSuccessStatusCode)
            {
                var errorBody = await pollResp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"AzureMaps matrix poll failed: {(int)pollResp.StatusCode} {pollResp.ReasonPhrase}. Body: {errorBody}");
            }

            return await pollResp.Content.ReadAsStringAsync(ct);
        }

        throw new TimeoutException("AzureMaps matrix polling exceeded max attempts.");
    }

    private int GetConfiguredPositiveInt(string key, int defaultValue)
    {
        var value = _cfg.GetSection(key)?.Value;
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static Dictionary<(string OriginId, string DestId), RouteResult> ParseMatrix(
       string body,
       List<string> originIds,
       List<string> destIds)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("AzureMaps matrix result body is empty.");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // The async poll endpoint occasionally wraps the response in a root array.
        if (root.ValueKind == JsonValueKind.Array)
        {
            if (root.GetArrayLength() == 0)
                throw new InvalidOperationException("AzureMaps matrix result root array is empty.");
            root = root[0];
        }

        // The Azure Route Matrix API returns:
        // {
        //   "matrix": [            ← outer array, one element per origin
        //     [                    ← inner array, one element per destination
        //       { "statusCode": 200, "response": { "routeSummary": { ... } } },
        //       ...
        //     ],
        //     ...
        //   ],
        //   "summary": { ... }
        // }
        //
        // Cells are identified purely by position (rowIdx = originIndex, colIdx = destIndex).
        // There are NO "originIndex" or "destinationIndex" properties on cells.
        if (!root.TryGetProperty("matrix", out var matrixRows) ||
            matrixRows.ValueKind != JsonValueKind.Array)
        {
            var sample = body.Length > 700 ? body[..700] + "..." : body;
            throw new InvalidOperationException(
                $"AzureMaps matrix response is missing the 'matrix' array. Body sample: {sample}");
        }

        var map = new Dictionary<(string OriginId, string DestId), RouteResult>();

        var rowIdx = 0;
        foreach (var row in matrixRows.EnumerateArray())
        {
            if (rowIdx >= originIds.Count)
                break;

            if (row.ValueKind != JsonValueKind.Array)
            {
                rowIdx++;
                continue;
            }

            var originId = originIds[rowIdx];
            var colIdx = 0;

            foreach (var cell in row.EnumerateArray())
            {
                if (colIdx >= destIds.Count)
                    break;

                var destId = destIds[colIdx];

                var statusCode = cell.TryGetProperty("statusCode", out var sc) ? sc.GetInt32() : 0;

                if (statusCode != 200 ||
                    !cell.TryGetProperty("response", out var resp) ||
                    resp.ValueKind != JsonValueKind.Object)
                {
                    // Cell present but not successful — record as low-confidence zero so the
                    // caller knows the pair was attempted. Not cached (see CachedRouteMatrixService).
                    map[(originId, destId)] = new RouteResult(0, 0, "Low", TravelRouteResolutionSource.AzureMaps);
                    colIdx++;
                    continue;
                }

                // Azure puts the route summary under either "routeSummary" or "summary".
                JsonElement summary;
                if (!resp.TryGetProperty("routeSummary", out summary) &&
                    !resp.TryGetProperty("summary", out summary))
                {
                    map[(originId, destId)] = new RouteResult(0, 0, "Low", TravelRouteResolutionSource.AzureMaps);
                    colIdx++;
                    continue;
                }

                var lengthMeters = summary.TryGetProperty("lengthInMeters", out var lm) ? lm.GetDouble() : 0d;
                var timeSeconds = summary.TryGetProperty("travelTimeInSeconds", out var ts) ? ts.GetInt32() : 0;

                var miles = Math.Round(lengthMeters / 1609.344d, 2);
                var minutes = (int)Math.Ceiling(timeSeconds / 60d);

                // Confidence is "High" whenever Azure returned a genuine route,
                // even if the route happens to be instantaneous (0 s).
                map[(originId, destId)] = new RouteResult(
                    minutes,
                    miles,
                    "High",
                    TravelRouteResolutionSource.AzureMaps);

                colIdx++;
            }

            rowIdx++;
        }

        return map;
    }
}
