namespace AFH.Location.Service.Core.Domain.Entities;

public sealed class Adviser
{
    public string AdviserId { get; set; } = default!;
    public string? CalendarUserId { get; set; }
    public string DisplayName { get; set; } = default!;
    public string HomePostcode { get; set; } = default!;
    public string Region { get; set; } = default!;
    public IReadOnlyCollection<string> Skills { get; set; } = Array.Empty<string>();
    public double Rating { get; set; }
    public bool IsActive { get; set; }
    public double? CoverageRadiusMiles { get; set; }
    public int? MaxTravelTimeMinutes { get; set; }
}
