namespace AFH.Adviser.Contract.V1.Availability;

public sealed record AdviserWorkingPatternRuleResponseV1(
    int Id,
    string AdviserId,
    string? DayOfWeek,
    string Start,
    string End,
    string? EffectiveFrom,
    string? EffectiveTo,
    bool IsActive);
