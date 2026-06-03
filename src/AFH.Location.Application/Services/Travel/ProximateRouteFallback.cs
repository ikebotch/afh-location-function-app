using AFH.Location.Application.Models.Travel;

namespace AFH.Location.Application.Services.Travel;

public static class ProximateRouteFallback
{
    private const double EarthRadiusMiles = 3958.7613d;
    private const double DefaultMaxFallbackDistanceMiles = 0.1d;
    private const double AssumedWalkingMilesPerHour = 3d;

    public static bool TryEstimate(
        LocationCoordinates source,
        LocationCoordinates destination,
        out ProximateRouteEstimate estimate)
    {
        var distanceMiles = CalculateDistanceMiles(source, destination);
        if (distanceMiles > DefaultMaxFallbackDistanceMiles)
        {
            estimate = default;
            return false;
        }

        var roundedDistance = Math.Round(distanceMiles, 2);
        var travelMinutes = distanceMiles <= 0d
            ? 0
            : Math.Max(1, (int)Math.Ceiling(distanceMiles / AssumedWalkingMilesPerHour * 60d));

        estimate = new ProximateRouteEstimate(travelMinutes, roundedDistance, "High");
        return true;
    }

    private static double CalculateDistanceMiles(LocationCoordinates source, LocationCoordinates destination)
    {
        var sourceLat = ToRadians(source.Latitude);
        var destinationLat = ToRadians(destination.Latitude);
        var deltaLat = ToRadians(destination.Latitude - source.Latitude);
        var deltaLon = ToRadians(destination.Longitude - source.Longitude);

        var a = Math.Sin(deltaLat / 2d) * Math.Sin(deltaLat / 2d)
                + Math.Cos(sourceLat)
                * Math.Cos(destinationLat)
                * Math.Sin(deltaLon / 2d)
                * Math.Sin(deltaLon / 2d);

        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return EarthRadiusMiles * c;
    }

    private static double ToRadians(double degrees)
        => degrees * Math.PI / 180d;
}

public readonly record struct ProximateRouteEstimate(
    int TravelTimeMinutes,
    double DistanceMiles,
    string Confidence);
