using System.Collections.Concurrent;
using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Models.V1;
using AFH.Location.Domain;
using AFH.Location.Domain.Entities;

namespace AFH.Location.Application.Services.V1;

public sealed class AdviserCoverageServiceV1 : IAdviserCoverageService
{
    private static readonly ConcurrentDictionary<string, (double Lat, double Lng)> CoordinateCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim CacheRefreshGate = new(1, 1);
    private static readonly TimeSpan CoverageCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromSeconds(20);
    private static DateTime _cachedAtUtc = DateTime.MinValue;
    private static AdviserCoverageResult? _cachedResponse;

    private readonly IAdviserRepository _adviserRepository;
    private readonly IOfficeRepository _officeRepository;
    private readonly IGeocodingService _geocodingService;
    private readonly ICoveragePolicyProvider _coveragePolicyProvider;
    private readonly ICoveragePresentationSettings _settings;

    public AdviserCoverageServiceV1(
        IAdviserRepository adviserRepository,
        IOfficeRepository officeRepository,
        IGeocodingService geocodingService,
        ICoveragePolicyProvider coveragePolicyProvider,
        ICoveragePresentationSettings settings)
    {
        _adviserRepository = adviserRepository;
        _officeRepository = officeRepository;
        _geocodingService = geocodingService;
        _coveragePolicyProvider = coveragePolicyProvider;
        _settings = settings;
    }

    public async Task<AdviserCoverageResult> GetCoverageAsync(CancellationToken ct)
    {
        if (_cachedResponse is not null && DateTime.UtcNow - _cachedAtUtc < CoverageCacheTtl)
            return _cachedResponse;

        await CacheRefreshGate.WaitAsync(ct);
        try
        {
            if (_cachedResponse is not null && DateTime.UtcNow - _cachedAtUtc < CoverageCacheTtl)
                return _cachedResponse;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(BuildTimeout);

            var response = await BuildCoverageAsync(timeoutCts.Token);
            _cachedResponse = response;
            _cachedAtUtc = DateTime.UtcNow;
            return response;
        }
        catch (OperationCanceledException) when (_cachedResponse is not null)
        {
            return _cachedResponse;
        }
        finally
        {
            CacheRefreshGate.Release();
        }
    }

    private async Task<AdviserCoverageResult> BuildCoverageAsync(CancellationToken ct)
    {
        var advisers = await _adviserRepository.GetAllAsync(null, ct);
        var coveragePolicy = await _coveragePolicyProvider.GetAsync(ct);
        var activeAdvisers = advisers.Where(x => x.IsActive).ToList();
        var offices = await _officeRepository.GetAllAsync(ct);

        var allPostcodes = activeAdvisers
            .Select(a => (a.HomePostcode ?? string.Empty).Trim())
            .Concat(offices.Select(o => (o.Postcode ?? string.Empty).Trim()))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var coordinates = await ResolveCoordinatesAsync(allPostcodes, ct);
        var adviserPoints = BuildAdviserPoints(activeAdvisers, coveragePolicy, coordinates);
        var regionPoints = BuildRegionPoints(offices, coordinates);

        return new AdviserCoverageResult
        {
            Advisers = adviserPoints.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            Regions = regionPoints.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private List<AdviserCoveragePoint> BuildAdviserPoints(
        IReadOnlyList<Adviser> advisers,
        CoveragePolicy coveragePolicy,
        IReadOnlyDictionary<string, (double Lat, double Lng)> coordinates)
    {
        var adviserPoints = new List<AdviserCoveragePoint>(advisers.Count);

        foreach (var adviser in advisers)
        {
            var postcode = (adviser.HomePostcode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(postcode))
                continue;

            if (!coordinates.TryGetValue(postcode, out var coord) || IsZero(coord))
                continue;

            var maxTravelTimeMinutes = ResolveMaxTravelTimeMinutes(adviser, coveragePolicy);
            var radius = ResolveRadiusMiles(adviser, maxTravelTimeMinutes, coveragePolicy, _settings.AverageTravelSpeedMph);

            adviserPoints.Add(new AdviserCoveragePoint
            {
                Id = adviser.AdviserId,
                Name = adviser.DisplayName,
                MailboxUserId = string.IsNullOrWhiteSpace(adviser.MailboxUserId) ? adviser.AdviserId : adviser.MailboxUserId.Trim(),
                Region = adviser.Region,
                Postcode = postcode,
                IsActive = adviser.IsActive,
                Skills = adviser.Skills.ToArray(),
                Rating = adviser.Rating,
                Latitude = coord.Lat,
                Longitude = coord.Lng,
                MaxTravelTimeMinutes = maxTravelTimeMinutes,
                RadiusMiles = radius.RadiusMiles,
                RadiusKm = Math.Max(1, (int)Math.Round(radius.RadiusMiles * 1.609344)),
                RadiusSource = radius.Source
            });
        }

        return adviserPoints;
    }

    private static List<RegionCoveragePoint> BuildRegionPoints(
        IReadOnlyList<Office> offices,
        IReadOnlyDictionary<string, (double Lat, double Lng)> coordinates)
    {
        var regionPoints = new List<RegionCoveragePoint>();

        foreach (var group in offices.GroupBy(x => x.Region, StringComparer.OrdinalIgnoreCase))
        {
            var coords = group
                .Select(office => coordinates.TryGetValue(office.Postcode, out var coord) ? coord : default)
                .Where(coord => !IsZero(coord))
                .ToList();

            if (coords.Count == 0)
                continue;

            regionPoints.Add(new RegionCoveragePoint
            {
                Id = group.Key.ToLowerInvariant().Replace(' ', '-'),
                Name = group.Key,
                Latitude = coords.Average(x => x.Lat),
                Longitude = coords.Average(x => x.Lng)
            });
        }

        return regionPoints;
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

        var value = await _geocodingService.GeocodeAsync($"{key}, United Kingdom", ct);
        if (IsZero(value))
            throw new InvalidOperationException($"Geocoding returned invalid coordinates for postcode '{key}'.");

        CoordinateCache[key] = value;
        return value;
    }

    private static int ResolveMaxTravelTimeMinutes(Adviser adviser, CoveragePolicy coveragePolicy)
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
        Adviser adviser,
        int maxTravelTimeMinutes,
        CoveragePolicy coveragePolicy,
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

    private static bool IsZero((double Lat, double Lng) value) => value.Lat == 0d && value.Lng == 0d;
}
