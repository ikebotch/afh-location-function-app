using AFH.Location.Application.Models.V1;
using AFH.Location.Application.Models.V1.Batch;
using AFH.Location.Application.Models.V1.Requests;
using AFH.Location.Application.Models.V1.Results;
using AFH.Location.Contract.V1.Requests;
using AFH.Location.Contract.V1.Responses;

namespace AFH.Location.Function.Mapping.V1;

public static class LocationContractMapper
{
    public static LocationSearchRequest ToApplicationRequest(LocationSearchRequestV1 contract)
    {
        return new LocationSearchRequest
        {
            RequestId = contract.RequestId,
            Meeting = new LocationMeetingWindow
            {
                RequestedStartUtc = contract.Meeting.RequestedStartUtc,
                DurationMinutes = contract.Meeting.DurationMinutes,
                SearchHorizonMinutes = contract.Meeting.SearchHorizonMinutes
            },
            Destination = new SearchDestination
            {
                Coordinates = contract.Destination.Coordinates is null
                    ? null
                    : new SearchCoordinates
                    {
                        Lat = contract.Destination.Coordinates.Lat,
                        Lng = contract.Destination.Coordinates.Lng
                    },
                Address = contract.Destination.Address is null
                    ? null
                    : new SearchAddress
                    {
                        Line1 = contract.Destination.Address.Line1,
                        Line2 = contract.Destination.Address.Line2,
                        Town = contract.Destination.Address.Town,
                        Postcode = contract.Destination.Address.Postcode,
                        Country = contract.Destination.Address.Country
                    }
            },
            Filters = new Application.Models.V1.Requests.LocationSearchFilters
            {
                Regions = contract.Filters?.Regions ?? [],
                AdviserIds = contract.Filters?.AdviserIds ?? [],
                RequiredSkills = contract.Filters?.RequiredSkills ?? [],
                PreferredAdviserIds = contract.Filters?.PreferredAdviserIds ?? [],
                ExcludeAdviserIds = contract.Filters?.ExcludeAdviserIds ?? [],
                MaxCandidates = contract.Filters?.MaxCandidates,
                BufferMinutes = contract.Filters?.BufferMinutes,
                CompanyBufferMinutes = contract.Filters?.CompanyBufferMinutes,
                MinAdviserRating = contract.Filters?.MinAdviserRating,
                MaxRankingScore = contract.Filters?.MaxRankingScore
            }
        };
    }

    public static LocationSearchResponseV1 ToContractResponse(LocationSearchResult result)
    {
        return new LocationSearchResponseV1
        {
            RequestId = result.RequestId,
            GeneratedAtUtc = result.GeneratedAtUtc,
            Candidates = result.Candidates.Select(ToContractCandidate).ToList(),
            Warnings = result.Warnings.Select(w => new ApiWarning
            {
                Code = w.Code,
                Message = w.Message
            }).ToList()
        };
    }

    public static AdviserCoverageResponseV1 ToContractResponse(AdviserCoverageResult result)
    {
        return new AdviserCoverageResponseV1
        {
            Advisers = result.Advisers.Select(x => new AdviserCoveragePointV1
            {
                Id = x.Id,
                Name = x.Name,
                MailboxUserId = x.MailboxUserId,
                Region = x.Region,
                Postcode = x.Postcode,
                IsActive = x.IsActive,
                Skills = x.Skills,
                Rating = x.Rating,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                MaxTravelTimeMinutes = x.MaxTravelTimeMinutes,
                RadiusMiles = x.RadiusMiles,
                RadiusKm = x.RadiusKm,
                RadiusSource = x.RadiusSource
            }).ToArray(),
            Regions = result.Regions.Select(x => new RegionCoveragePointV1
            {
                Id = x.Id,
                Name = x.Name,
                Latitude = x.Latitude,
                Longitude = x.Longitude
            }).ToArray()
        };
    }

    public static LicenseListResponseV1 ToContractResponse(LicenseCatalogResult result)
    {
        return new LicenseListResponseV1
        {
            Licenses = result.Licenses
        };
    }

    public static LocationSearchBatchResponseV1 ToContractResponse(LocationSearchBatchResult result)
    {
        return new LocationSearchBatchResponseV1
        {
            GeneratedAtUtc = result.GeneratedAtUtc,
            Results = result.Results.Select(x => new LocationSearchBatchItemResponseV1
            {
                RequestId = x.RequestId,
                Success = x.Success,
                ErrorCode = x.ErrorCode,
                ErrorMessage = x.ErrorMessage,
                Result = x.Result is null ? null : ToContractResponse(x.Result)
            }).ToList()
        };
    }

    private static LocationCandidate ToContractCandidate(LocationSearchCandidate candidate)
    {
        return new LocationCandidate
        {
            AdviserId = candidate.AdviserId,
            MailboxUserId = candidate.MailboxUserId,
            AdviserRating = candidate.AdviserRating,
            GoldStar = candidate.GoldStar,
            Preferred = candidate.Preferred,
            Availability = candidate.Availability,
            ProposedSlotUtc = new ProposedSlot
            {
                Start = candidate.ProposedSlotUtc.Start,
                End = candidate.ProposedSlotUtc.End
            },
            Coverage = new CoverageInfo
            {
                WithinCoverage = candidate.Coverage.WithinCoverage,
                AnchorPostcode = candidate.Coverage.AnchorPostcode,
                DistanceMiles = candidate.Coverage.DistanceMiles
            },
            TravelToClient = new TravelToClient
            {
                EtaMinutes = candidate.TravelToClient.EtaMinutes,
                DistanceMiles = candidate.TravelToClient.DistanceMiles,
                Confidence = candidate.TravelToClient.Confidence
            },
            TravelToBase = new TravelToBase
            {
                HomeMinutes = candidate.TravelToBase.HomeMinutes,
                OfficeMinutes = candidate.TravelToBase.OfficeMinutes
            },
            TravelToNearestOffice = new TravelToNearestOffice
            {
                OfficeId = candidate.TravelToNearestOffice.OfficeId,
                EtaMinutes = candidate.TravelToNearestOffice.EtaMinutes,
                DistanceMiles = candidate.TravelToNearestOffice.DistanceMiles,
                Confidence = candidate.TravelToNearestOffice.Confidence
            },
            Buffers = new BufferInfo
            {
                TravelBufferMinutes = candidate.Buffers.TravelBufferMinutes,
                CompanyBufferMinutes = candidate.Buffers.CompanyBufferMinutes,
                PreMeetingBufferMinutes = candidate.Buffers.PreMeetingBufferMinutes,
                PostMeetingBufferMinutes = candidate.Buffers.PostMeetingBufferMinutes,
                MaxTravelTimeMinutes = candidate.Buffers.MaxTravelTimeMinutes
            },
            TravelSnapshot = candidate.TravelSnapshot is null
                ? null
                : new AFH.Location.Contract.V1.Responses.TravelSnapshotResult
                {
                    SourceLocationRef = candidate.TravelSnapshot.SourceLocationRef,
                    SourcePostcode = candidate.TravelSnapshot.SourcePostcode,
                    DestinationLocationRef = candidate.TravelSnapshot.DestinationLocationRef,
                    DestinationPostcode = candidate.TravelSnapshot.DestinationPostcode,
                    TravelMinutes = candidate.TravelSnapshot.TravelMinutes,
                    DistanceMiles = candidate.TravelSnapshot.DistanceMiles,
                    Provider = candidate.TravelSnapshot.Provider,
                    Confidence = candidate.TravelSnapshot.Confidence,
                    CalculatedUtc = candidate.TravelSnapshot.CalculatedUtc
                },
            Reasons = candidate.Reasons.ToList(),
            Rank = candidate.Rank,
            Score = candidate.Score
        };
    }
}