namespace AFH.Location.Application.Models.Travel;

public sealed record RouteTimeResult
{
    public string? CorrelationId { get; init; }
    public int? TravelTimeMinutes { get; init; }
    public double? TravelDistanceMiles { get; init; }
    public RouteTimeStatus Status { get; init; }
    public IReadOnlyList<TravelCoverageWarning> Warnings { get; init; } = [];
}
