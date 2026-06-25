namespace AFH.Adviser.Contract.V1.Availability;

public sealed record AdviserAvailabilityRulesResponseV1(
    int MinimumAppointmentMinutes,
    string DefaultWorkingDayStart,
    string DefaultWorkingDayEnd,
    int CapacityWindowDays,
    IReadOnlyList<AdviserWorkingPatternRuleResponseV1> WorkingPatterns,
    IReadOnlyList<AdviserCapacityLimitRuleResponseV1> CapacityLimits);

