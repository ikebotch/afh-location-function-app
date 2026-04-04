namespace AFH.Location.Contract.V1.Requests;

public sealed class LocationSearchRequestV1
{
    public string RequestId { get; set; } = default!;
    public MeetingWindow Meeting { get; set; } = new();
    public Destination Destination { get; set; } = new();


    public LocationSearchFilters? Filters { get; init; }
}
