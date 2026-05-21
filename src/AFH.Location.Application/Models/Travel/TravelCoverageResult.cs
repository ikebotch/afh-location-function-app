namespace AFH.Location.Application.Models.Travel;

public sealed record TravelCoverageResult
{
    public string SourcePostcode { get; init; } = string.Empty;
    public LocationCoordinates? SourceCoordinates { get; init; }
    public TravelCoverageTimeContext TimeContext { get; init; } = new();
    public IReadOnlyList<TravelCoverageDestinationOutcome> Destinations { get; init; } = [];
    public LocationRequestContext RequestContext { get; init; } = new();
}
