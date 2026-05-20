using AFH.Location.Application.Models.V1.Travel;
using AFH.Location.Domain.Travel;

namespace AFH.Location.Application.Validation.V1;

public static class TravelCoverageRequestValidatorV1
{
    public static List<string> Validate(TravelCoverageRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.SourcePostcode))
            errors.Add("sourcePostcode is required.");

        if (request.TimeContext.TimingMode == TravelCoverageTimingMode.DepartureTime
            && request.TimeContext.RequestedDepartureTime is null)
        {
            errors.Add("timeContext.requestedDepartureTime is required when timingMode is DepartureTime.");
        }

        if (request.Destinations.Count == 0)
            errors.Add("destinations is required and cannot be empty.");

        for (var i = 0; i < request.Destinations.Count; i++)
        {
            var destination = request.Destinations[i];
            if (string.IsNullOrWhiteSpace(destination.CorrelationId))
                errors.Add($"destinations[{i}].correlationId is required.");

            if (string.IsNullOrWhiteSpace(destination.Postcode))
                errors.Add($"destinations[{i}].postcode is required.");

            if (destination.MaxTravelTimeMinutes is <= 0)
                errors.Add($"destinations[{i}].maxTravelTimeMinutes must be greater than zero when supplied.");

            if (destination.MaxDistanceMiles is <= 0)
                errors.Add($"destinations[{i}].maxDistanceMiles must be greater than zero when supplied.");
        }

        return errors;
    }
}
