namespace AFH.Adviser.Application.Models.Availability;

public sealed class AdviserCapacityLimitRule
{
    public string AdviserId { get; init; } = string.Empty;
    public int MaxActiveBookings { get; init; }
    public int? DailyLimit { get; init; }
    public int? WeeklyLimit { get; init; }
    public int? MonthlyLimit { get; init; }
}
