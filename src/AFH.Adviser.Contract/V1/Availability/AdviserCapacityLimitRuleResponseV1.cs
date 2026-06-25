namespace AFH.Adviser.Contract.V1.Availability;

public sealed record AdviserCapacityLimitRuleResponseV1(
    string AdviserId,
    int MaxActiveBookings,
    int? DailyLimit,
    int? WeeklyLimit,
    int? MonthlyLimit);
