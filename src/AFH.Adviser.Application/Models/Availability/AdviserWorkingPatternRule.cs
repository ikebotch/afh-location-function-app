namespace AFH.Adviser.Application.Models.Availability;

public sealed class AdviserWorkingPatternRule
{
    public string AdviserId { get; init; } = string.Empty;
    public string Start { get; init; } = string.Empty;
    public string End { get; init; } = string.Empty;
}
