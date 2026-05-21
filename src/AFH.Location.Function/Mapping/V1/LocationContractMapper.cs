using AFH.Location.Application.Models.V1;
using AFH.Location.Application.Models.V1.Travel;
using AFH.Location.Contract.V1.Requests.Travel;
using AFH.Location.Contract.V1.Responses;
using AFH.Location.Contract.V1.Responses.Travel;
using AFH.Location.Domain.Travel;
using ContractTravelCoverageStatus = AFH.Location.Contract.V1.Responses.Travel.TravelCoverageStatusV1;
using ContractTravelEvaluationMode = AFH.Location.Contract.V1.Requests.Travel.TravelEvaluationModeV1;
using ContractSlotResponseMode = AFH.Location.Contract.V1.Requests.Travel.SlotResponseModeV1;
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
                RequestedDepartureTime = contract.TimeContext.StartTime,
                TimingMode = contract.TimeContext.TravelEvaluationMode == ContractTravelEvaluationMode.TimeDependent
                    ? TravelCoverageTimingMode.DepartureTime
                    : TravelCoverageTimingMode.TimeIndependent,
                SlotResponseMode = contract.TimeContext.SlotResponseMode switch
                {
                    ContractSlotResponseMode.Expanded => TravelCoverageSlotResponseMode.Expanded,
                    ContractSlotResponseMode.Summary => TravelCoverageSlotResponseMode.Summary,
                    _ => TravelCoverageSlotResponseMode.Grouped
                },
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
            Metadata = new TravelCoverageRequestMetadata(),
            RequestContext = new LocationRequestContext
            {
                CorrelationId = contract.RequestContext.CorrelationId,
                RequestedBy = null
            }
        };
    }

    public static TravelCoverageResponseV1 ToContractResponse(TravelCoverageResult result)
    {
        return new TravelCoverageResponseV1
        {
            SourcePostcode = result.SourcePostcode,
            SourceCoordinates = ToContractCoordinates(result.SourceCoordinates),
            TimeContext = new TravelCoverageTimeContextV1
            {
                TravelEvaluationMode = result.TimeContext.TimingMode == TravelCoverageTimingMode.DepartureTime
                    ? ContractTravelEvaluationMode.TimeDependent
                    : ContractTravelEvaluationMode.TimeIndependent,
                SlotResponseMode = result.TimeContext.SlotResponseMode switch
                {
                    TravelCoverageSlotResponseMode.Expanded => ContractSlotResponseMode.Expanded,
                    TravelCoverageSlotResponseMode.Summary => ContractSlotResponseMode.Summary,
                    _ => ContractSlotResponseMode.Grouped
                },
                StartTime = result.TimeContext.StartTime,
                EndTime = result.TimeContext.EndTime,
                SearchIntervalMinutes = result.TimeContext.SearchIntervalMinutes
            },
            Destinations = result.Destinations.Select(destination => new TravelCoverageDestinationOutcomeV1
            {
                CorrelationId = destination.CorrelationId,
                Postcode = destination.Postcode,
                Coordinates = ToContractCoordinates(destination.Coordinates),
                Status = ToContractStatus(destination.Status),
                Slots = MapSlots(result.TimeContext.SlotResponseMode, result.TimeContext, destination),
                Warnings = destination.Warnings != null
                    ? destination.Warnings.Select(warning => new ApiWarning
                      {
                          Code = warning.Code,
                          Message = warning.Message
                      }).ToList()
                    : new List<ApiWarning>()
            }).ToList(),
            RequestContext = new LocationRequestContextV1
            {
                CorrelationId = result.RequestContext.CorrelationId
            }
        };
    }

    public static RouteTimeRequest ToApplicationRequest(RouteTimeRequestV1 contract)
    {
        return new RouteTimeRequest
        {
            CorrelationId = contract.CorrelationId,
            DepartAt = contract.DepartAt,
            Source = new LocationCoordinates(contract.Source.Latitude, contract.Source.Longitude),
            Destination = new LocationCoordinates(contract.Destination.Latitude, contract.Destination.Longitude)
        };
    }

    public static RouteTimeResponseV1 ToContractResponse(RouteTimeResult result)
    {
        return new RouteTimeResponseV1
        {
            CorrelationId = result.CorrelationId,
            TravelTimeMinutes = result.TravelTimeMinutes,
            TravelDistanceMiles = result.TravelDistanceMiles,
            Status = result.Status switch
            {
                RouteTimeStatus.Succeeded => RouteTimeStatusV1.Succeeded,
                RouteTimeStatus.RouteUnavailable => RouteTimeStatusV1.RouteUnavailable,
                _ => RouteTimeStatusV1.Failed
            },
            Warnings = result.Warnings.Select(warning => new ApiWarning
            {
                Code = warning.Code,
                Message = warning.Message
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

    private static TravelCoverageCoordinatesV1? ToContractCoordinates(LocationCoordinates? coordinates)
    {
        return coordinates is null
            ? null
            : new TravelCoverageCoordinatesV1
            {
                Latitude = coordinates.Latitude,
                Longitude = coordinates.Longitude
            };
    }

    private static IReadOnlyList<TravelCoverageSlotV1>? MapSlots(
        TravelCoverageSlotResponseMode responseMode,
        TravelCoverageTimeContext timeContext,
        TravelCoverageDestinationOutcome destination)
    {
        if (destination.Status != ApplicationTravelCoverageStatus.Succeeded)
        {
            return null;
        }

        var sourceSlots = destination.Slots;
        if ((sourceSlots == null || sourceSlots.Count == 0) && destination.Route != null)
        {
            sourceSlots = new List<TravelCoverageSlotOutcome>
            {
                new()
                {
                    StartTime = timeContext.RequestedDepartureTime ?? timeContext.StartTime,
                    EndTime = timeContext.EndTime,
                    Route = destination.Route,
                    Coverage = destination.Coverage
                }
            };
        }

        if (sourceSlots == null || sourceSlots.Count == 0)
        {
            return [];
        }

        var rawSlots = sourceSlots.Select(s => new TravelCoverageSlotV1
        {
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            TravelTimeMinutes = s.Route?.TravelTimeMinutes ?? 0,
            TravelDistanceMiles = s.Route?.DistanceMiles ?? 0d,
            IsWithinCoverage = s.Coverage?.IsWithinCoverage ?? false
        }).ToList();

        if (responseMode == TravelCoverageSlotResponseMode.Expanded)
        {
            return rawSlots;
        }

        if (responseMode == TravelCoverageSlotResponseMode.Summary)
        {
            var minStart = rawSlots.Min(s => s.StartTime) ?? timeContext.StartTime;
            var maxEnd = rawSlots.Max(s => s.EndTime) ?? timeContext.EndTime;
            var maxTravelTime = rawSlots.Count > 0 ? rawSlots.Max(s => s.TravelTimeMinutes) : 0;
            var maxDistance = rawSlots.Count > 0 ? rawSlots.Max(s => s.TravelDistanceMiles) : 0d;
            var allWithinCoverage = rawSlots.Count > 0 && rawSlots.All(s => s.IsWithinCoverage);

            return new List<TravelCoverageSlotV1>
            {
                new()
                {
                    StartTime = minStart,
                    EndTime = maxEnd,
                    TravelTimeMinutes = maxTravelTime,
                    TravelDistanceMiles = maxDistance,
                    IsWithinCoverage = allWithinCoverage
                }
            };
        }

        // Default: Grouped
        var mergedSlots = new List<TravelCoverageSlotV1>();
        var currentMerged = rawSlots[0];
        for (int i = 1; i < rawSlots.Count; i++)
        {
            var nextSlot = rawSlots[i];
            if (currentMerged.TravelTimeMinutes == nextSlot.TravelTimeMinutes &&
                currentMerged.TravelDistanceMiles == nextSlot.TravelDistanceMiles &&
                currentMerged.IsWithinCoverage == nextSlot.IsWithinCoverage)
            {
                currentMerged = currentMerged with { EndTime = nextSlot.EndTime };
            }
            else
            {
                mergedSlots.Add(currentMerged);
                currentMerged = nextSlot;
            }
        }
        mergedSlots.Add(currentMerged);

        return mergedSlots;
    }
}
