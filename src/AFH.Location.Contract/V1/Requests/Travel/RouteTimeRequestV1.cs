namespace AFH.Location.Contract.V1.Requests.Travel;

public sealed record RouteTimeRequestV1
{
    public string? CorrelationId { get; init; }
    public DateTimeOffset DepartAt { get; init; }
    public RouteTimeCoordinatesV1 Source { get; init; } = new();
    public RouteTimeCoordinatesV1 Destination { get; init; } = new();
}

public sealed record RouteTimeCoordinatesV1
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
