using AFH.Adviser.Application.Models.Feed;
using AFH.Adviser.Contract.V1.Responses;

namespace AFH.Location.Function.Mapping.V1.Adviser;

public static class AdviserCoverageContractMapper
{
    public static AdviserCoverageResponseV1 ToContractResponse(AdviserFeedResult result)
    {
        return new AdviserCoverageResponseV1
        {
            Advisers = result.Advisers.Select(ToContractPoint).ToList(),
            Regions = result.Regions.Select(ToContractRegion).ToList()
        };
    }

    private static AdviserCoveragePointV1 ToContractPoint(AdviserFeedItem adviser)
    {
        return new AdviserCoveragePointV1
        {
            Id = adviser.Id,
            Name = adviser.Name,
            MailboxUserId = adviser.MailboxUserId,
            Region = adviser.Region,
            Postcode = adviser.Postcode,
            IsActive = adviser.IsActive,
            Skills = adviser.Skills,
            Rating = adviser.Rating,
            Latitude = adviser.Latitude,
            Longitude = adviser.Longitude,
            MaxTravelTimeMinutes = adviser.MaxTravelTimeMinutes,
            RadiusMiles = adviser.RadiusMiles,
            RadiusKm = adviser.RadiusKm,
            RadiusSource = adviser.RadiusSource
        };
    }

    private static RegionCoveragePointV1 ToContractRegion(AdviserFeedRegionItem region)
    {
        return new RegionCoveragePointV1
        {
            Id = region.Id,
            Name = region.Name,
            Latitude = region.Latitude,
            Longitude = region.Longitude
        };
    }
}
