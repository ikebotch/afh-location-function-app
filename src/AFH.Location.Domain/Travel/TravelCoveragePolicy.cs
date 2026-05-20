namespace AFH.Location.Domain.Travel;

public sealed record TravelCoveragePolicy(
    int? MaxTravelTimeMinutes,
    double? MaxDistanceMiles)
{
    public CoverageDecision Evaluate(TravelRouteOutcome outcome)
    {
        if (!outcome.HasUsableRoute)
            return CoverageDecision.RouteUnavailable();

        var withinTime = !MaxTravelTimeMinutes.HasValue
            || outcome.TravelTimeMinutes <= MaxTravelTimeMinutes.Value;

        var withinDistance = !MaxDistanceMiles.HasValue
            || outcome.DistanceMiles <= MaxDistanceMiles.Value;

        return CoverageDecision.Completed(withinTime && withinDistance);
    }
}

public sealed record TravelRouteOutcome(
    int? TravelTimeMinutes,
    double? DistanceMiles,
    string? Confidence,
    TravelRouteResolutionSource ResolutionSource)
{
    public bool HasUsableRoute => TravelTimeMinutes.HasValue && DistanceMiles.HasValue;
}

public sealed record CoverageDecision(
    bool IsWithinCoverage,
    CoverageDecisionStatus Status)
{
    public static CoverageDecision Completed(bool isWithinCoverage)
        => new(isWithinCoverage, CoverageDecisionStatus.Completed);

    public static CoverageDecision RouteUnavailable()
        => new(false, CoverageDecisionStatus.RouteUnavailable);
}

public enum CoverageDecisionStatus
{
    Completed = 0,
    RouteUnavailable = 1
}

public enum TravelRouteResolutionSource
{
    Unknown = 0,
    Cache = 1,
    Database = 2,
    AzureMaps = 3
}

public enum TravelCoverageTimingMode
{
    TimeIndependent = 0,
    DepartureTime = 1
}
