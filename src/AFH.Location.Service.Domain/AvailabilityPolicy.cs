namespace AFH.Location.Service.Domain;

public sealed class AvailabilityPolicy
{
    public int DefaultBufferMinutes { get; set; } = 0;
    public int MaxBufferMinutes { get; set; } = 180;
    public int DefaultCompanyBufferMinutes { get; set; } = 30;
    public int MaxCompanyBufferMinutes { get; set; } = 180;
    public int PreviousClientProximityMinutes { get; set; } = 180;
    public bool RequireCalendarAvailability { get; set; } = true;
}
