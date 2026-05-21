using AFH.Adviser.Application.Models.Feed;
using AFH.Adviser.Application.Abstractions.Feed;
using AFH.Adviser.Application.Abstractions.Repositories;
using AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Application.Services.Feed;

public sealed class AdviserFeedService : IAdviserFeedService
{
    private readonly IAdviserRepository _advisers;

    public AdviserFeedService(IAdviserRepository advisers)
    {
        _advisers = advisers;
    }

    public async Task<AdviserFeedResult> GetCoverageFeedAsync(CancellationToken ct)
    {
        var advisers = await _advisers.GetAllAsync(null, ct);
        var activeAdvisers = advisers.Where(x => x.IsActive).ToList();

        var adviserPoints = activeAdvisers.Select(MapAdviser).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        
        var regionPoints = activeAdvisers
            .Where(x => !string.IsNullOrWhiteSpace(x.Region))
            .GroupBy(x => x.Region!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new AdviserFeedRegionItem
            {
                Id = g.Key.ToLowerInvariant().Replace(' ', '-'),
                Name = g.Key,
                Latitude = 0,
                Longitude = 0
            })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AdviserFeedResult
        {
            Advisers = adviserPoints,
            Regions = regionPoints
        };
    }

    private static AdviserFeedItem MapAdviser(Domain.Entities.Adviser adviser)
    {
        var radiusMiles = adviser.CoverageRadiusMiles ?? 0d;
        
        return new AdviserFeedItem
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
