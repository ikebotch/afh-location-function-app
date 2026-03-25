using AFH.Location.Service.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AFH.Location.Service.Infrastructure.External.Maps.Azure;

public sealed class AzureMapsRouteMatrixService : IRouteMatrixService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public AzureMapsRouteMatrixService(IHttpClientFactory httpFactory, IConfiguration cfg)
    {
        _http = httpFactory.CreateClient(nameof(AzureMapsRouteMatrixService));
        _cfg = cfg;
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

        // Call matrix
        var matrix = await ExecuteMatrixAsync(origins, destinations, ct);

        // Extract each adviser -> DEST cell
        var results = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var adviserId in adviserOrigins.Keys)
        {
            if (matrix.TryGetValue((adviserId, "DEST"), out var rr))
                results[adviserId] = rr;
            else
                results[adviserId] = new RouteResult(0, 0, "Low");
        }

        return results;
    }

    /// <summary>
    /// One origin -> many destinations (used for TravelToBase and nearest office).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
        (double Lat, double Lng) origin,
        IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
        CancellationToken ct)
    {
        if (destinations.Count == 0)
            return new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);

        var origins = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ORIGIN"] = origin
        };

        var matrix = await ExecuteMatrixAsync(origins, destinations, ct);

        // Extract ORIGIN -> each destination id
        var results = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var destId in destinations.Keys)
        {
            if (matrix.TryGetValue(("ORIGIN", destId), out var rr))
                results[destId] = rr;
            else
                results[destId] = new RouteResult(0, 0, "Low");
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
        // Simple polling with max attempts. You can wire this to your policy provider later.
        const int maxAttempts = 10;
        const int delayMs = 400;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var pollResp = await _http.GetAsync(pollUrl, ct);

            if (pollResp.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                await Task.Delay(delayMs, ct);
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

    private static Dictionary<(string OriginId, string DestId), RouteResult> ParseMatrix(
       string body,
       List<string> originIds,
       List<string> destIds)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("AzureMaps matrix result body is empty.");

        using var doc = JsonDocument.Parse(body);

        // Azure sometimes returns root as array
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            if (root.GetArrayLength() == 0)
                throw new InvalidOperationException("AzureMaps matrix result root array is empty.");

            root = root[0];
        }

        // matrix can be object or array
        JsonElement matrixEl;
        if (root.TryGetProperty("matrix", out matrixEl))
        {
            if (matrixEl.ValueKind == JsonValueKind.Array)
            {
                if (matrixEl.GetArrayLength() == 0)
                    throw new InvalidOperationException("AzureMaps matrix array is empty.");

                matrixEl = matrixEl[0];
            }
        }
        else
        {
            // Some responses may have results at the root
            matrixEl = root;
        }

        // results can live under matrix.results OR root.results OR matrix itself can be results array
        JsonElement resultsEl;

        if (matrixEl.ValueKind == JsonValueKind.Array)
        {
            resultsEl = matrixEl;
        }
        else if (matrixEl.ValueKind == JsonValueKind.Object && matrixEl.TryGetProperty("results", out resultsEl))
        {
            // ok
        }
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("results", out resultsEl))
        {
            // fallback
        }
        else
        {
            var sample = body.Length > 700 ? body[..700] + "..." : body;
            throw new InvalidOperationException($"AzureMaps matrix response shape unexpected. Body sample: {sample}");
        }

        if (resultsEl.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("AzureMaps matrix results is not an array.");

        var map = new Dictionary<(string OriginId, string DestId), RouteResult>();

        foreach (var item in resultsEl.EnumerateArray())
        {
            // Defensive checks
            if (!item.TryGetProperty("originIndex", out var oi) ||
                !item.TryGetProperty("destinationIndex", out var di))
                continue;

            var oIdx = oi.GetInt32();
            var dIdx = di.GetInt32();

            if (oIdx < 0 || oIdx >= originIds.Count || dIdx < 0 || dIdx >= destIds.Count)
                continue;

            var originId = originIds[oIdx];
            var destId = destIds[dIdx];

            var statusCode = item.TryGetProperty("statusCode", out var sc) ? sc.GetInt32() : 0;

            if (statusCode != 200)
            {
                map[(originId, destId)] = new RouteResult(0, 0, "Low");
                continue;
            }

            // Azure can return summary under response.routeSummary or response.summary (depending on endpoint/version)
            if (!item.TryGetProperty("response", out var resp) || resp.ValueKind != JsonValueKind.Object)
            {
                map[(originId, destId)] = new RouteResult(0, 0, "Low");
                continue;
            }

            JsonElement summary;
            if (!(resp.TryGetProperty("routeSummary", out summary) || resp.TryGetProperty("summary", out summary)))
            {
                map[(originId, destId)] = new RouteResult(0, 0, "Low");
                continue;
            }

            var lengthMeters = summary.TryGetProperty("lengthInMeters", out var lm) ? lm.GetDouble() : 0d;

            // travelTimeInSeconds sometimes appears as travelTimeInSeconds or travelTimeInSeconds (same) – keep defensive
            var timeSeconds =
                summary.TryGetProperty("travelTimeInSeconds", out var ts) ? ts.GetInt32() :
                summary.TryGetProperty("travelTimeInSeconds", out ts) ? ts.GetInt32() :
                0;

            var miles = lengthMeters / 1609.344d;
            var minutes = (int)Math.Ceiling(timeSeconds / 60d);

            map[(originId, destId)] = new RouteResult(
                minutes,
                Math.Round(miles, 2),
                minutes > 0 ? "High" : "Low");
        }

        return map;
    }
}