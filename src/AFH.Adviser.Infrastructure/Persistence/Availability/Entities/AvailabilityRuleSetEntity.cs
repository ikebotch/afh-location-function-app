namespace AFH.Adviser.Infrastructure.Persistence.Availability.Entities;

public sealed class AvailabilityRuleSetEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProjectContext { get; set; } = "Booking";
    public bool IsActive { get; set; } = true;
    public int MinimumAppointmentMinutes { get; set; } = 1;
    public string DefaultWorkingDayStart { get; set; } = "08:00";
    public string DefaultWorkingDayEnd { get; set; } = "17:00";
    public int CapacityWindowDays { get; set; } = 1;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    public List<AdviserWorkingPatternRuleEntity> WorkingPatterns { get; set; } = [];
    public List<AdviserCapacityLimitRuleEntity> CapacityLimits { get; set; } = [];
}
