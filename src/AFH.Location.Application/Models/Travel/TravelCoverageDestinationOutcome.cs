using AFH.Location.Domain.Travel;

namespace AFH.Location.Application.Models.Travel;

public sealed record TravelCoverageDestinationOutcome
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public TravelCoverageStatus Status { get; init; }
    public LocationCoordinates? Coordinates { get; init; }
    public TravelRouteOutcome? Route { get; init; }
    public TravelCoverageOutcome? Coverage { get; init; }
    public IReadOnlyList<TravelCoverageSlotOutcome> Slots { get; init; } = [];
    public IReadOnlyList<TravelCoveragePresentedSlot>? PresentedSlots { get; init; }
    public IReadOnlyList<TravelCoverageWarning> Warnings { get; init; } = [];
}
