namespace AFH.Location.Application.Models.V1;

public sealed class LocationMeetingWindow
{
    public DateTime RequestedStartUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int SearchHorizonMinutes { get; set; }
}
