namespace AFH.Location.Application.Models.V1.Results;

public sealed class BufferResult
{
    public int TravelBufferMinutes { get; set; }
    public int CompanyBufferMinutes { get; set; }
    public int PreMeetingBufferMinutes { get; set; }
    public int PostMeetingBufferMinutes { get; set; }
    public int MaxTravelTimeMinutes { get; set; }
}
