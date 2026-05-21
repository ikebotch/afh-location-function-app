using AFH.Location.Application.Models.Travel;

using AFH.Location.Domain.Travel;

namespace AFH.Location.Application.Validation.Travel;

public static class TravelCoverageRequestValidator
{
    public static List<string> Validate(TravelCoverageRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.SourcePostcode))
            errors.Add("sourcePostcode is required.");

        if (request.TimeContext.StartTime is null)
            errors.Add("timeContext.startTime is required.");

        if (request.TimeContext.EndTime is null)
            errors.Add("timeContext.endTime is required.");

        if (request.TimeContext.StartTime.HasValue && request.TimeContext.EndTime.HasValue && request.TimeContext.EndTime.Value <= request.TimeContext.StartTime.Value)
            errors.Add("timeContext.endTime must be after timeContext.startTime.");

        if (request.TimeContext.SearchIntervalMinutes is null)
            errors.Add("timeContext.searchIntervalMinutes is required.");
        else if (request.TimeContext.SearchIntervalMinutes <= 0)
            errors.Add("timeContext.searchIntervalMinutes must be greater than zero.");

        if (request.TimeContext.StartTime.HasValue && request.TimeContext.EndTime.HasValue && request.TimeContext.StartTime.Value < request.TimeContext.EndTime.Value)
        {
            var interval = request.TimeContext.SearchIntervalMinutes ?? 30;
            if (interval > 0)
            {
                var count = 0;
                var current = request.TimeContext.StartTime.Value;
                var end = request.TimeContext.EndTime.Value;
                while (current < end)
                {
                    count++;
                    current = current.AddMinutes(interval);
                }
                if (count > 24)
                {
                    errors.Add("The requested time range generates too many slots. Maximum allowed is 24 slots.");
                }
            }
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
