namespace AFH.Adviser.Application.Models.Availability;

public sealed class AdviserAvailabilityRules
{
    public int MinimumAppointmentMinutes { get; init; } = 1;
    public string DefaultWorkingDayStart { get; init; } = "08:00";
    public string DefaultWorkingDayEnd { get; init; } = "17:00";
    public int CapacityWindowDays { get; init; } = 1;
    public IReadOnlyList<AdviserWorkingPatternRule> WorkingPatterns { get; init; } = [];
    public IReadOnlyList<AdviserCapacityLimitRule> CapacityLimits { get; init; } = [];
}
