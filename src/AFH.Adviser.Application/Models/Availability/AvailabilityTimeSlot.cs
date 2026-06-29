namespace AFH.Adviser.Application.Models.Availability;

public sealed class AvailabilityTimeSlot
{
    public string Id { get; init; } = string.Empty;
    public string AdviserId { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
    public bool IsBooked { get; init; }
    public string? BookingId { get; init; }
    public string Status { get; init; } = "Available";
}
