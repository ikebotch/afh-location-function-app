namespace AFH.Location.Application.Models.Travel;

public sealed record TravelCoverageRequestMetadata
{
    public string? AppointmentType { get; init; }
    public string? Channel { get; init; }
}
