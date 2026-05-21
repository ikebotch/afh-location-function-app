using AFH.Location.Domain.Travel;

namespace AFH.Location.Application.Models.Travel;

public sealed record TravelCoverageSlotOutcome
{
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public TravelRouteOutcome? Route { get; init; }
    public TravelCoverageOutcome? Coverage { get; init; }
}
