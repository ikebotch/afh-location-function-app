namespace AFH.Location.Service.Core.Services.Common;

public sealed class AvailabilityPolicy
{
    public int DefaultBufferMinutes { get; set; } = 0;
    public int MaxBufferMinutes { get; set; } = 180;
}
