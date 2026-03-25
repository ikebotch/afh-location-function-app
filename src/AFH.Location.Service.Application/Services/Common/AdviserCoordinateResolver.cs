using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Domain.Entities;
using System.Text.RegularExpressions;

namespace AFH.Location.Service.Application.Services.Common;

public sealed class AdviserCoordinateResolver
{
    private readonly IAdviserGeoCache _cache;
    private readonly IGeocodingService _geocoding;
    private readonly IGeoCachePolicyProvider _policyProvider;

    public AdviserCoordinateResolver(
        IAdviserGeoCache cache,
        IGeocodingService geocoding,
        IGeoCachePolicyProvider policyProvider)
    {
        _cache = cache;
        _geocoding = geocoding;
        _policyProvider = policyProvider;
    }

    public async Task<(double Lat, double Lng)> ResolveHomeAsync(
        Adviser adviser,
        CancellationToken ct,
        string? postcodeOverride = null)
    {
        var policy = await _policyProvider.GetAsync(ct);

        var postcode = NormalisePostcode(postcodeOverride ?? adviser.HomePostcode);

        // Version the key so old cached “bad” values don’t keep poisoning results
        var cacheKey = $"adviser:home:v2:{adviser.AdviserId}:{postcode}".ToLowerInvariant();

        if (_cache.TryGet(cacheKey, out var cached))
            return cached;

        // 1) Validate postcode format (guards partial / nonsense / legacy placeholders)
        if (!LooksLikeUkPostcode(postcode))
        {
            _cache.Set(cacheKey, (0d, 0d), policy.FailureTtl);
            return (0d, 0d);
        }

        // 2) Geocode with a strong query
        (double Lat, double Lng) resolved;
        try
        {
            var query = $"{postcode}, United Kingdom";
            resolved = await _geocoding.GeocodeAsync(query, ct);
        }
        catch
        {
            resolved = (0d, 0d);
        }

        // 3) Cache result (failure TTL vs success TTL)
        var ttl = (resolved.Lat == 0d && resolved.Lng == 0d)
            ? policy.FailureTtl
            : policy.AdviserHomeTtl;

        _cache.Set(cacheKey, resolved, ttl);

        return resolved;
    }

    private static string NormalisePostcode(string? postcode)
        => (postcode ?? string.Empty).Trim().ToUpperInvariant();

    private static bool LooksLikeUkPostcode(string postcode)
    {
        if (string.IsNullOrWhiteSpace(postcode))
            return false;

        // Add the standard space before the inward code if it’s missing (e.g. "B11AA" -> "B1 1AA")
        // This helps the regex accept valid postcodes entered without a space.
        postcode = EnsureInwardSpace(postcode);

        // Strict-enough UK postcode pattern for validation (does not guarantee “in use”)
        // Examples: "B3 2BB", "SW1A 1AA", "EC1A 1BB", "W1A 0AX", "M1 1AE", "GIR 0AA"
        return Regex.IsMatch(
            postcode,
            @"^(GIR 0AA|[A-Z]{1,2}\d[A-Z\d]? \d[A-Z]{2})$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private static string EnsureInwardSpace(string postcode)
    {
        postcode = postcode.Trim().ToUpperInvariant();

        // If already has a space, leave it
        if (postcode.Contains(' '))
            return postcode;

        // UK inward code is always 3 chars (e.g. "1AA"), so insert a space 3 from the end
        if (postcode.Length > 3)
            return postcode.Insert(postcode.Length - 3, " ");

        return postcode;
    }
}