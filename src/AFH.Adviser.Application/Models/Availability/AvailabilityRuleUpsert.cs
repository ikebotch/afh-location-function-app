namespace AFH.Adviser.Application.Models.Availability;

public sealed class AvailabilityRuleUpsert
{
    public string? ProjectContext { get; init; }
    public string AdviserId { get; init; } = string.Empty;
    public string? AdviserName { get; init; }
    public string? DayOfWeek { get; init; }
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public string? EffectiveFrom { get; init; }
    public string? EffectiveTo { get; init; }
    public string? Status { get; init; }
    public string? Notes { get; init; }
}
