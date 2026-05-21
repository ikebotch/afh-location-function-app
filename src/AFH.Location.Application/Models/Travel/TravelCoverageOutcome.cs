namespace AFH.Location.Application.Models.Travel;

public sealed record TravelCoverageOutcome
{
    public bool IsWithinCoverage { get; init; }
    public int? MaxTravelTimeMinutes { get; init; }
    public double? MaxDistanceMiles { get; init; }
}
