namespace AFH.Location.Application.Travel;

public sealed record TravelCoveragePresentedSlot
{
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public int TravelTimeMinutes { get; init; }
    public double TravelDistanceMiles { get; init; }
    public bool IsWithinCoverage { get; init; }
}
