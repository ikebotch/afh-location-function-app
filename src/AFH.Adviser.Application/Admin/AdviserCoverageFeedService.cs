using AFH.Adviser.Application.Abstractions;

namespace AFH.Adviser.Application.Admin;

public sealed class AdviserCoverageFeedService : IAdviserCoverageFeedService
{
    private readonly IAdviserRepository _adviserRepository;

    public AdviserCoverageFeedService(IAdviserRepository adviserRepository)
    {
        _adviserRepository = adviserRepository;
    }

    public async Task<AdviserCoverageFeedResult> GetCoverageFeedAsync(DateTime? sinceUtc, CancellationToken ct)
    {
        var advisers = await _adviserRepository.GetAllAsync(null, ct);

        if (sinceUtc.HasValue)
        {
            advisers = advisers.Where(x => x.LastSyncedUtc >= sinceUtc.Value).ToList();
        }

        var activeAdvisers = advisers.Where(x => x.IsActive).ToList();

        var adviserPoints = activeAdvisers.Select(MapAdviser).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        
        var regionPoints = activeAdvisers
            .Where(x => !string.IsNullOrWhiteSpace(x.Region))
            .GroupBy(x => x.Region!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new AdviserCoverageFeedRegion
            {
                Id = g.Key.ToLowerInvariant().Replace(' ', '-'),
                Name = g.Key,
                Latitude = 0,
                Longitude = 0
            })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AdviserCoverageFeedResult
        {
            Advisers = adviserPoints,
            Regions = regionPoints
        };
    }

    private static AdviserCoverageFeedAdviser MapAdviser(Domain.Entities.Adviser adviser)
    {
        var radiusMiles = adviser.CoverageRadiusMiles ?? 0d;
        
        return new AdviserCoverageFeedAdviser
        {
            Id = adviser.AdviserId,
            Name = adviser.DisplayName,
            MailboxUserId = string.IsNullOrWhiteSpace(adviser.MailboxUserId) ? adviser.AdviserId : adviser.MailboxUserId.Trim(),
            Region = adviser.Region,
            Postcode = (adviser.HomePostcode ?? string.Empty).Trim(),
            IsActive = adviser.IsActive,
            Skills = adviser.Skills.ToArray(),
            Rating = adviser.Rating,
            Latitude = 0,
            Longitude = 0,
            MaxTravelTimeMinutes = adviser.MaxTravelTimeMinutes ?? 0,
            RadiusMiles = radiusMiles,
            RadiusKm = Math.Max(1, (int)Math.Round(radiusMiles * 1.609344)),
            RadiusSource = adviser.CoverageRadiusMiles.HasValue ? "SharePointRadiusMiles" : "Default"
        };
    }
}
