namespace AFH.Location.Domain.Entities;

public sealed class Adviser
{
    public string AdviserId { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string MailboxUserId { get; set; } = string.Empty;
    public string HomePostcode { get; set; } = default!;
    public string Region { get; set; } = default!;
    public string? BaseOfficeId { get; set; }
    public string? TeamName { get; set; }
    public string? ManagerId { get; set; }
    public IReadOnlyCollection<string> Skills { get; set; } = Array.Empty<string>();
    public double Rating { get; set; }
    public bool IsActive { get; set; }
    public bool IsBookable { get; set; } = true;
    public double? CoverageRadiusMiles { get; set; }
    public int? MaxTravelTimeMinutes { get; set; }
    public DateTime? LastSyncedUtc { get; set; }
}
