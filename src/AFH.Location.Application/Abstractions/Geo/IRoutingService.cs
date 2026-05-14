namespace AFH.Location.Application.Abstractions.Geo;

public interface IRoutingService
{

    Task<RouteResult> GetRouteAsync(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        CancellationToken ct);
}

public sealed record RouteResult(int EtaMinutes, double DistanceMiles, string Confidence);