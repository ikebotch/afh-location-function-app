namespace AFH.Location.Application.Models.Travel;

public sealed record TravelRouteOutcomeRequest
{
    public LocationCoordinates Source { get; init; } = new(0, 0);
    public IReadOnlyDictionary<string, LocationCoordinates> Destinations { get; init; } =
        new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase);
    public TravelCoverageTimeContext TimeContext { get; init; } = new();
}
