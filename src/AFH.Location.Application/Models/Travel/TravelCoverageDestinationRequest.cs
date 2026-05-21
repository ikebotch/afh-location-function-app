namespace AFH.Location.Application.Models.Travel;

public sealed record TravelCoverageDestinationRequest
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public int? MaxTravelTimeMinutes { get; init; }
    public double? MaxDistanceMiles { get; init; }
}
