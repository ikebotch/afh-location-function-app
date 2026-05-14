namespace AFH.Location.Application.Models.V1.Requests;

public sealed class LocationSearchRequest
{
    public string RequestId { get; set; } = default!;
    public LocationMeetingWindow Meeting { get; set; } = new();
    public SearchDestination Destination { get; set; } = new();
    public LocationSearchFilters Filters { get; set; } = new();
}
