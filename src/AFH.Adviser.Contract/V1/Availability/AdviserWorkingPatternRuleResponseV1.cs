namespace AFH.Adviser.Contract.V1.Availability;

public sealed record AdviserWorkingPatternRuleResponseV1(
    string AdviserId,
    string Start,
    string End);

