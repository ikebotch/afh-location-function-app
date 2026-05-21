using AFH.Location.Application.Travel;
using AFH.Location.Contract.V1.Requests.Travel;
using AFH.Location.Contract.V1.Responses;
using AFH.Location.Contract.V1.Responses.Travel;

namespace AFH.Location.Function.Mapping.V1;

public static class RouteTimeContractMapper
{
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
}
