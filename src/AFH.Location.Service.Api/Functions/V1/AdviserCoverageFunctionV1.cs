using System.Collections.Concurrent;
using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Contracts.V1.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Service.Api.Functions.V1;

public sealed class AdviserCoverageFunctionV1
{
    private static readonly ConcurrentDictionary<string, (double Lat, double Lng)> CoordinateCache = new(StringComparer.OrdinalIgnoreCase);
    private const double DefaultAverageTravelSpeedMph = 35d;
    private static readonly SemaphoreSlim CacheRefreshGate = new(1, 1);
    private static readonly TimeSpan CoverageCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromSeconds(20);
    private static DateTime _cachedAtUtc = DateTime.MinValue;
    private static AdviserCoverageResponseV1? _cachedResponse;

    private readonly IAdviserRepository _adviserRepository;
    private readonly IOfficeRepository _officeRepository;
    private readonly IGeocodingService _geocodingService;
    private readonly ICoveragePolicyProvider _coveragePolicyProvider;
    private readonly IConfiguration _configuration;

    public AdviserCoverageFunctionV1(
        IAdviserRepository adviserRepository,
        IOfficeRepository officeRepository,
        IGeocodingService geocodingService,
        ICoveragePolicyProvider coveragePolicyProvider,
        IConfiguration configuration)
    {
        _adviserRepository = adviserRepository;
        _officeRepository = officeRepository;
        _geocodingService = geocodingService;
        _coveragePolicyProvider = coveragePolicyProvider;
        _configuration = configuration;
    }

    [Function("AdviserCoverageV1")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/adviser-coverage")]
        HttpRequestData req,
        CancellationToken ct)
    {
        if (_cachedResponse is not null && DateTime.UtcNow - _cachedAtUtc < CoverageCacheTtl)
            return await req.WriteSuccessAsync(_cachedResponse, ct, ApiEnvelopeExtensions.SinglePage(_cachedResponse.Advisers.Count));

        await CacheRefreshGate.WaitAsync(ct);
        try
        {
            if (_cachedResponse is not null && DateTime.UtcNow - _cachedAtUtc < CoverageCacheTtl)
                return await req.WriteSuccessAsync(_cachedResponse, ct, ApiEnvelopeExtensions.SinglePage(_cachedResponse.Advisers.Count));

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(BuildTimeout);

            var response = await BuildCoverageAsync(timeoutCts.Token);
            _cachedResponse = response;
            _cachedAtUtc = DateTime.UtcNow;

            return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Advisers.Count));
        }
        catch (OperationCanceledException) when (_cachedResponse is not null)
        {
            return await req.WriteSuccessAsync(_cachedResponse, ct, ApiEnvelopeExtensions.SinglePage(_cachedResponse.Advisers.Count));
        }
        finally
        {
            CacheRefreshGate.Release();
        }
    }

    private async Task<AdviserCoverageResponseV1> BuildCoverageAsync(CancellationToken ct)
    {
        var advisers = await _adviserRepository.GetAllAsync(null, ct);
        var coveragePolicy = await _coveragePolicyProvider.GetAsync(ct);
        var averageTravelSpeedMph =
            _configuration.GetValue<double?>("LocationSearch:Coverage:AverageTravelSpeedMph")
            ?? DefaultAverageTravelSpeedMph;

        var activeAdvisers = advisers.Where(x => x.IsActive).ToList();
        var adviserPostcodes = activeAdvisers
            .Select(a => (a.HomePostcode ?? string.Empty).Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var offices = await _officeRepository.GetAllAsync(ct);
        var officePostcodes = offices
            .Select(o => (o.Postcode ?? string.Empty).Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var allPostcodes = adviserPostcodes
            .Concat(officePostcodes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var coordinates = await ResolveCoordinatesAsync(allPostcodes, ct);

        var adviserPoints = new List<AdviserCoveragePointV1>(activeAdvisers.Count);
        foreach (var adviser in activeAdvisers)
        {
            var postcode = (adviser.HomePostcode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(postcode))
                continue;

            if (!coordinates.TryGetValue(postcode, out var coord))
                continue;

            if (coord.Lat == 0 && coord.Lng == 0)
                continue;

            var maxTravelTimeMinutes = ResolveMaxTravelTimeMinutes(adviser, coveragePolicy);
            var radiusResult = ResolveRadiusMiles(
                adviser,
                maxTravelTimeMinutes,
                coveragePolicy,
                averageTravelSpeedMph);

            adviserPoints.Add(new AdviserCoveragePointV1
            {
                Id = adviser.AdviserId,
                Name = adviser.DisplayName,
                Region = adviser.Region,
                Postcode = postcode,
                Latitude = coord.Lat,
                Longitude = coord.Lng,
                MaxTravelTimeMinutes = maxTravelTimeMinutes,
                RadiusMiles = radiusResult.RadiusMiles,
                RadiusKm = Math.Max(1, (int)Math.Round(radiusResult.RadiusMiles * 1.609344)),
                RadiusSource = radiusResult.Source
            });
        }

        var regionPoints = new List<RegionCoveragePointV1>();
        foreach (var group in offices.GroupBy(x => x.Region, StringComparer.OrdinalIgnoreCase))
        {
            var coords = new List<(double Lat, double Lng)>();
            foreach (var office in group)
            {
                if (!coordinates.TryGetValue(office.Postcode, out var coord))
                    continue;

                if (coord.Lat == 0 && coord.Lng == 0)
                    continue;
                coords.Add(coord);
            }

            if (coords.Count == 0)
                continue;

            regionPoints.Add(new RegionCoveragePointV1
            {
                Id = group.Key.ToLowerInvariant().Replace(' ', '-'),
                Name = group.Key,
                Latitude = coords.Average(x => x.Lat),
                Longitude = coords.Average(x => x.Lng)
            });
        }

        return new AdviserCoverageResponseV1
        {
            Advisers = adviserPoints.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            Regions = regionPoints.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private async Task<Dictionary<string, (double Lat, double Lng)>> ResolveCoordinatesAsync(
        IReadOnlyList<string> postcodes,
        CancellationToken ct)
    {
        var result = new ConcurrentDictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);
        var throttler = new SemaphoreSlim(6, 6);

        var tasks = postcodes.Select(async postcode =>
        {
            await throttler.WaitAsync(ct);
            try
            {
                var value = await ResolveSingleCoordinateAsync(postcode, ct);
                result[postcode] = value;
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks);
        return result.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<(double Lat, double Lng)> ResolveSingleCoordinateAsync(string postcode, CancellationToken ct)
    {
        var key = postcode.Trim().ToUpperInvariant();
        if (CoordinateCache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var value = await _geocodingService.GeocodeAsync($"{key}, United Kingdom", ct);
            CoordinateCache[key] = value;
            return value;
        }
        catch
        {
            CoordinateCache[key] = (0, 0);
            return (0, 0);
        }
    }

    private static int ResolveMaxTravelTimeMinutes(
        Core.Domain.Entities.Adviser adviser,
        Core.Services.Common.CoveragePolicy coveragePolicy)
    {
        if (adviser.MaxTravelTimeMinutes is > 0)
            return adviser.MaxTravelTimeMinutes.Value;

        if (coveragePolicy.AdviserMaxTravelTimeMinutes.TryGetValue(adviser.AdviserId, out var adviserMax) && adviserMax > 0)
            return adviserMax;

        if (!string.IsNullOrWhiteSpace(adviser.Region)
            && coveragePolicy.RegionMaxTravelTimeMinutes.TryGetValue(adviser.Region, out var regionMax)
            && regionMax > 0)
            return regionMax;

        return Math.Max(1, coveragePolicy.DefaultMaxTravelTimeMinutes);
    }

    private static (double RadiusMiles, string Source) ResolveRadiusMiles(
        Core.Domain.Entities.Adviser adviser,
        int maxTravelTimeMinutes,
        Core.Services.Common.CoveragePolicy coveragePolicy,
        double averageTravelSpeedMph)
    {
        if (adviser.CoverageRadiusMiles is > 0)
            return (adviser.CoverageRadiusMiles.Value, "SharePointRadiusMiles");

        if (coveragePolicy.AdviserRadiusMiles.TryGetValue(adviser.AdviserId, out var adviserRadius) && adviserRadius > 0)
            return (adviserRadius, "PolicyAdviserRadiusMiles");

        if (!string.IsNullOrWhiteSpace(adviser.Region)
            && coveragePolicy.RegionRadiusMiles.TryGetValue(adviser.Region, out var regionRadius)
            && regionRadius > 0)
            return (regionRadius, "PolicyRegionRadiusMiles");

        var travelDerivedMiles = maxTravelTimeMinutes * Math.Max(1d, averageTravelSpeedMph) / 60d;
        if (travelDerivedMiles > 0)
            return (travelDerivedMiles, "TravelTimeDerivedMiles");

        return (Math.Max(1, coveragePolicy.DefaultRadiusMiles), "PolicyDefaultRadiusMiles");
    }
}
