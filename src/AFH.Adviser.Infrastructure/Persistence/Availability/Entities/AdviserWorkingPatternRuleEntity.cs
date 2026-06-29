namespace AFH.Adviser.Infrastructure.Persistence.Availability.Entities;

public sealed class AdviserWorkingPatternRuleEntity
{
    public int Id { get; set; }
    public int RuleSetId { get; set; }
    public string AdviserId { get; set; } = string.Empty;
    public string? DayOfWeek { get; set; }
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    public AvailabilityRuleSetEntity? RuleSet { get; set; }
}
