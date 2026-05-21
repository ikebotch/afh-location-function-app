using AFH.Location.Application.Travel;
using AFH.Location.Contract.V1.Requests.Travel;
using AFH.Location.Contract.V1.Responses;
using AFH.Location.Contract.V1.Responses.Travel;
using AFH.Location.Domain.Travel;
using ApplicationTravelCoverageStatus = AFH.Location.Application.Travel.TravelCoverageStatus;
using ContractSlotResponseMode = AFH.Location.Contract.V1.Requests.Travel.SlotResponseModeV1;
using ContractTravelCoverageStatus = AFH.Location.Contract.V1.Responses.Travel.TravelCoverageStatusV1;
using ContractTravelEvaluationMode = AFH.Location.Contract.V1.Requests.Travel.TravelEvaluationModeV1;

namespace AFH.Location.Function.Mapping.V1;

public static class TravelCoverageContractMapper
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
                    : []
            }).ToList(),
            RequestContext = new LocationRequestContextV1
            {
                CorrelationId = result.RequestContext.CorrelationId
            }
        };
    }

    private static IReadOnlyList<TravelCoverageSlotV1>? MapSlots(
        TravelCoverageSlotResponseMode responseMode,
        TravelCoverageTimeContext timeContext,
        TravelCoverageDestinationOutcome destination)
    {
        return TravelCoverageResponsePresenter.PresentSlots(responseMode, timeContext, destination)
            ?.Select(s => new TravelCoverageSlotV1
            {
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                TravelTimeMinutes = s.TravelTimeMinutes,
                TravelDistanceMiles = s.TravelDistanceMiles,
                IsWithinCoverage = s.IsWithinCoverage
            })
            .ToList();
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
}
