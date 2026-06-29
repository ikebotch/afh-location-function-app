namespace AFH.Adviser.Application.Models.Availability;

public sealed class AvailabilityTimeSlotsQuery
{
    public string? ProjectContext { get; init; }
    public string? AdviserId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}
