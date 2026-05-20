namespace AFH.Location.Application.Abstractions.Geo;

public interface IRouteMatrixService
{
    Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
        IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
        (double Lat, double Lng) destination,
        CancellationToken ct);



    Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
        (double Lat, double Lng) origin,
        IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
        DateTimeOffset? departAt = null,
        CancellationToken ct = default);
}