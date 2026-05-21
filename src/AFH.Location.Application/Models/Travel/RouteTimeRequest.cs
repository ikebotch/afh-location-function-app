namespace AFH.Location.Application.Models.Travel;

public sealed record RouteTimeRequest
{
    public string? CorrelationId { get; init; }
    public DateTimeOffset DepartAt { get; init; }
    public LocationCoordinates Source { get; init; } = new(0, 0);
    public LocationCoordinates Destination { get; init; } = new(0, 0);
}
