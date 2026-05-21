namespace AFH.Location.Application.Models.Travel;

public sealed record TravelCoverageRequest
{
    public string SourcePostcode { get; init; } = string.Empty;
    public TravelCoverageTimeContext TimeContext { get; init; } = new();
    public IReadOnlyList<TravelCoverageDestinationRequest> Destinations { get; init; } = [];
    public TravelCoverageRequestMetadata Metadata { get; init; } = new();
    public LocationRequestContext RequestContext { get; init; } = new();
}
