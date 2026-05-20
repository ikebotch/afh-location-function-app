using System.Collections.Concurrent;
using AFH.Location.Application.Abstractions.Advisers;
using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Abstractions.Search;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Application.Models.V1;
using AFH.Location.Domain;
using AFH.Location.Domain.Entities;

namespace AFH.Location.Application.Services.V1;

public sealed class AdviserCoverageService : IAdviserCoverageService
{
    private readonly IAdviserRepository _adviserRepository;
    private readonly IOfficeRepository _officeRepository;
    private readonly IPostcodeCoordinateResolver _postcodeCoordinateResolver;
    private readonly ICoveragePolicyProvider _coveragePolicyProvider;
    private readonly ICoveragePresentationSettings _settings;

    public AdviserCoverageService(
        IAdviserRepository adviserRepository,
        IOfficeRepository officeRepository,
        IPostcodeCoordinateResolver postcodeCoordinateResolver,
        ICoveragePolicyProvider coveragePolicyProvider,
        ICoveragePresentationSettings settings)
    {
        _adviserRepository = adviserRepository;
        _officeRepository = officeRepository;
        _postcodeCoordinateResolver = postcodeCoordinateResolver;
        _coveragePolicyProvider = coveragePolicyProvider;
        _settings = settings;
    }

    public async Task<AdviserCoverageResult> GetCoverageAsync(CancellationToken ct)
    {
        return await BuildCoverageAsync(ct);
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
        var postcodeMap = postcodes.ToDictionary(p => p, p => p, StringComparer.OrdinalIgnoreCase);
        var resolutions = await _postcodeCoordinateResolver.ResolveManyAsync(postcodeMap, ct);
        var coordinates = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);

        foreach (var postcode in postcodes)
        {
            if (!resolutions.TryGetValue(postcode, out var res) || res.Coordinates == null)
            {
                throw new InvalidOperationException($"Geocoding returned invalid coordinates for postcode '{postcode}'.");
            }
            coordinates[postcode] = (res.Coordinates.Latitude, res.Coordinates.Longitude);
        }

        return coordinates;
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
