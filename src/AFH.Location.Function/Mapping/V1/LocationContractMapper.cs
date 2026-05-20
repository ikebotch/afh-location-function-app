using AFH.Location.Application.Models.V1;
using AFH.Location.Application.Models.V1.Batch;
using AFH.Location.Application.Models.V1.Requests;
using AFH.Location.Application.Models.V1.Results;
using AFH.Location.Application.Models.V1.Travel;
using AFH.Location.Contract.V1.Requests;
using AFH.Location.Contract.V1.Requests.Travel;
using AFH.Location.Contract.V1.Responses;
using AFH.Location.Contract.V1.Responses.Travel;
using AFH.Location.Domain.Travel;
using ContractTravelCoverageStatus = AFH.Location.Contract.V1.Responses.Travel.TravelCoverageStatusV1;
using ContractTravelCoverageTimingMode = AFH.Location.Contract.V1.Requests.Travel.TravelCoverageTimingModeV1;
using ApplicationTravelCoverageStatus = AFH.Location.Application.Models.V1.Travel.TravelCoverageStatus;

namespace AFH.Location.Function.Mapping.V1;

public static class LocationContractMapper
{
    public static TravelCoverageRequest ToApplicationRequest(TravelCoverageRequestV1 contract)
    {
        return new TravelCoverageRequest
        {
            SourcePostcode = contract.SourcePostcode,
            TimeContext = new TravelCoverageTimeContext
            {
                RequestedDepartureTime = contract.TimeContext.RequestedDepartureTime,
                TimingMode = contract.TimeContext.TimingMode == ContractTravelCoverageTimingMode.DepartureTime
                    ? TravelCoverageTimingMode.DepartureTime
                    : TravelCoverageTimingMode.TimeIndependent,
                StartTime = contract.TimeContext.StartTime,
                EndTime = contract.TimeContext.EndTime,
                SearchIntervalMinutes = contract.TimeContext.SearchIntervalMinutes
            },
            Destinations = contract.Destinations.Select(destination => new TravelCoverageDestinationRequest
            {
                CorrelationId = destination.CorrelationId,
                Postcode = destination.Postcode,
                MaxTravelTimeMinutes = destination.MaxTravelTimeMinutes,
                MaxDistanceMiles = destination.MaxDistanceMiles
            }).ToList(),
            Metadata = new TravelCoverageRequestMetadata
            {
                AppointmentType = contract.Metadata.AppointmentType,
                Channel = contract.Metadata.Channel
            },
            RequestContext = new LocationRequestContext
            {
                CorrelationId = contract.RequestContext.CorrelationId,
                RequestedBy = contract.RequestContext.RequestedBy
            }
        };
    }

    public static TravelCoverageResponseV1 ToContractResponse(TravelCoverageResult result)
    {
        return new TravelCoverageResponseV1
        {
            SourcePostcode = result.SourcePostcode,
            TimeContext = new TravelCoverageTimeContextV1
            {
                RequestedDepartureTime = result.TimeContext.RequestedDepartureTime,
                TimingMode = result.TimeContext.TimingMode == TravelCoverageTimingMode.DepartureTime
                    ? ContractTravelCoverageTimingMode.DepartureTime
                    : ContractTravelCoverageTimingMode.TimeIndependent,
                StartTime = result.TimeContext.StartTime,
                EndTime = result.TimeContext.EndTime,
                SearchIntervalMinutes = result.TimeContext.SearchIntervalMinutes
            },
            Destinations = result.Destinations.Select(destination => new TravelCoverageDestinationOutcomeV1
            {
                CorrelationId = destination.CorrelationId,
                Postcode = destination.Postcode,
                Status = ToContractStatus(destination.Status),
                Slots = GenerateSlots(result.TimeContext, destination),
                Warnings = destination.Status == ApplicationTravelCoverageStatus.Succeeded
                    ? null
                    : destination.Warnings.Any()
                        ? destination.Warnings.Select(warning => new ApiWarning
                          {
                              Code = warning.Code,
                              Message = warning.Message
                          }).ToList()
                        : null
            }).ToList(),
            RequestContext = new LocationRequestContextV1
            {
                CorrelationId = result.RequestContext.CorrelationId,
                RequestedBy = result.RequestContext.RequestedBy
            }
        };
    }

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

    private static ContractTravelCoverageStatus ToContractStatus(ApplicationTravelCoverageStatus status)
    {
        return status switch
        {
            ApplicationTravelCoverageStatus.Succeeded => ContractTravelCoverageStatus.Succeeded,
            ApplicationTravelCoverageStatus.SourcePostcodeUnresolved => ContractTravelCoverageStatus.SourcePostcodeUnresolved,
            ApplicationTravelCoverageStatus.DestinationPostcodeUnresolved => ContractTravelCoverageStatus.DestinationPostcodeUnresolved,
            ApplicationTravelCoverageStatus.RouteUnavailable => ContractTravelCoverageStatus.RouteUnavailable,
            _ => ContractTravelCoverageStatus.Failed
        };
    }

    private static IReadOnlyList<TravelCoverageSlotV1>? GenerateSlots(
        TravelCoverageTimeContext timeContext,
        TravelCoverageDestinationOutcome destination)
    {
        if (destination.Status != ApplicationTravelCoverageStatus.Succeeded)
        {
            return null;
        }

        var travelTimeMinutes = destination.Route?.TravelTimeMinutes ?? 0;
        var travelDistanceMiles = destination.Route?.DistanceMiles ?? 0d;
        var isWithinCoverage = destination.Coverage?.IsWithinCoverage ?? false;

        if (timeContext.StartTime.HasValue && timeContext.EndTime.HasValue)
        {
            var slots = new List<TravelCoverageSlotV1>();
            var start = timeContext.StartTime.Value;
            var end = timeContext.EndTime.Value;
            var interval = timeContext.SearchIntervalMinutes;

            if (interval.HasValue && interval.Value > 0)
            {
                var current = start;
                while (current < end)
                {
                    var next = current.AddMinutes(interval.Value);
                    if (next > end)
                    {
                        next = end;
                    }

                    slots.Add(new TravelCoverageSlotV1
                    {
                        StartTime = current,
                        EndTime = next,
                        TravelTimeMinutes = travelTimeMinutes,
                        TravelDistanceMiles = travelDistanceMiles,
                        IsWithinCoverage = isWithinCoverage
                    });

                    current = next;
                }
            }
            else
            {
                slots.Add(new TravelCoverageSlotV1
                {
                    StartTime = start,
                    EndTime = end,
                    TravelTimeMinutes = travelTimeMinutes,
                    TravelDistanceMiles = travelDistanceMiles,
                    IsWithinCoverage = isWithinCoverage
                });
            }

            return slots;
        }

        // Fallback/Departure Time/Missing window
        return new List<TravelCoverageSlotV1>
        {
            new()
            {
                StartTime = timeContext.RequestedDepartureTime,
                EndTime = null,
                TravelTimeMinutes = travelTimeMinutes,
                TravelDistanceMiles = travelDistanceMiles,
                IsWithinCoverage = isWithinCoverage
            }
        };
    }
}
