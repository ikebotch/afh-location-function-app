using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Domain.Entities;

namespace AFH.Location.Service.Application.Services.Common;

public sealed class OfficeCoordinateResolver
{
    private readonly IOfficeRepository _repo;
    private readonly IGeocodingService _geocoding;
    private readonly IGeoCachePolicyProvider _policyProvider;
    private readonly IGeoCache _cache; 

    public OfficeCoordinateResolver(
        IOfficeRepository repo,
        IGeocodingService geocoding,
        IGeoCachePolicyProvider policyProvider,
        IGeoCache cache)
    {
        _repo = repo;
        _geocoding = geocoding;
        _policyProvider = policyProvider;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<string, (double Lat, double Lng)>> GetOfficeCoordsAsync(CancellationToken ct)
    {
        var policy = await _policyProvider.GetAsync(ct);
        var offices = await _repo.GetAllAsync(ct);

        var result = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);

        foreach (var o in offices)
        {
            var key = $"office:v1:{o.OfficeId}:{o.Postcode}".ToLowerInvariant();

            if (_cache.TryGet(key, out var cached))
            {
                result[o.OfficeId] = cached;
                continue;
            }

            var coords = await _geocoding.GeocodeAsync($"{o.Postcode}, United Kingdom", ct);

            var ttl = (coords.Lat == 0d && coords.Lng == 0d)
                ? policy.FailureTtl
                : policy.SuccessTtl;

            _cache.Set(key, coords, ttl);

            result[o.OfficeId] = coords;
        }

        return result;
    }
}