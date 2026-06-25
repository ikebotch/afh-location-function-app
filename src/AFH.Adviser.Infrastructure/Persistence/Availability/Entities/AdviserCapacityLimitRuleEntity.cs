namespace AFH.Adviser.Infrastructure.Persistence.Availability.Entities;

public sealed class AdviserCapacityLimitRuleEntity
{
    public int Id { get; set; }
    public int RuleSetId { get; set; }
    public string AdviserId { get; set; } = string.Empty;
    public int MaxActiveBookings { get; set; }
    public int? DailyLimit { get; set; }
    public int? WeeklyLimit { get; set; }
    public int? MonthlyLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    public AvailabilityRuleSetEntity? RuleSet { get; set; }
}
