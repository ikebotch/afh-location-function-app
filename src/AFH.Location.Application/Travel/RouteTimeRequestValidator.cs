using AFH.Location.Application.Travel;

namespace AFH.Location.Application.Travel;

public static class RouteTimeRequestValidator
{
    public static IReadOnlyList<string> Validate(RouteTimeRequest request)
    {
        var errors = new List<string>();

        if (request.DepartAt == default)
            errors.Add("departAt is required.");

        AddCoordinateErrors(request.Source, "source", errors);
        AddCoordinateErrors(request.Destination, "destination", errors);

        return errors;
    }

    private static void AddCoordinateErrors(LocationCoordinates coordinates, string fieldName, List<string> errors)
    {
        if (coordinates.Latitude is < -90 or > 90)
            errors.Add($"{fieldName}.latitude must be between -90 and 90.");

        if (coordinates.Longitude is < -180 or > 180)
            errors.Add($"{fieldName}.longitude must be between -180 and 180.");
    }
}
