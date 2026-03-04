using AFH.Location.Service.Core.Contracts.V1.Requests;

namespace AFH.Location.Service.Core.Validation.V1;

public static class LocationSearchRequestValidatorV1
{
    public static List<string> Validate(LocationSearchRequestV1 req)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(req.RequestId))
            errors.Add("requestId is required.");

        if (req.Meeting is null)
            errors.Add("meeting is required.");
        else
        {
            if (req.Meeting.DurationMinutes <= 0) errors.Add("meeting.durationMinutes must be > 0.");
            if (req.Meeting.SearchHorizonMinutes <= 0) errors.Add("meeting.searchHorizonMinutes must be > 0.");
            if (req.Meeting.RequestedStartUtc == default) errors.Add("meeting.requestedStartUtc is required.");
        }

        if (req.Destination is null)
            errors.Add("destination is required.");
        else
        {
            var hasCoords = req.Destination.Coordinates is not null;
            var hasAddress = req.Destination.Address is not null;

            if (!hasCoords && !hasAddress)
                errors.Add("destination.coordinates or destination.address must be provided.");

            if (hasAddress)
            {
                var a = req.Destination.Address!;
                if (string.IsNullOrWhiteSpace(a.Line1)) errors.Add("destination.address.line1 is required.");
                if (string.IsNullOrWhiteSpace(a.Town)) errors.Add("destination.address.town is required.");
                if (string.IsNullOrWhiteSpace(a.Postcode)) errors.Add("destination.address.postcode is required.");
                if (string.IsNullOrWhiteSpace(a.Country)) errors.Add("destination.address.country is required.");
            }
        }

        return errors;
    }
}