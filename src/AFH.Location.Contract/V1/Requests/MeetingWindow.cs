namespace AFH.Location.Contract.V1.Requests;

public sealed class MeetingWindow
{
    public DateTime RequestedStartUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int SearchHorizonMinutes { get; set; }
}
