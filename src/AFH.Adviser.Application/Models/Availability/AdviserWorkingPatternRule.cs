namespace AFH.Adviser.Application.Models.Availability;

public sealed class AdviserWorkingPatternRule
{
    public int Id { get; init; }
    public string AdviserId { get; init; } = string.Empty;
    public string? DayOfWeek { get; init; }
    public string Start { get; init; } = string.Empty;
    public string End { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}
