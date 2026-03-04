namespace AFH.Location.Service.Core.Services.Common;

public static class CoverageEvaluator
{
    // Simple haversine in miles
    //ToDo: Get fast; routing distance  from matrix later
    public static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3958.7613; // Earth radius in miles
        double ToRad(double d) => d * (Math.PI / 180);

        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);

        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}