namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;

public sealed class AdviserReferenceCacheEntity
{
    public string AdviserId { get; set; } = string.Empty;
    public string? XPlanAdviserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string MailboxUserId { get; set; } = string.Empty;
    public string HomePostcode { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string? BaseOfficeId { get; set; }
    public string? TeamName { get; set; }
    public string? ManagerId { get; set; }
    public string SkillsCsv { get; set; } = string.Empty;
    public double Rating { get; set; }
    public bool IsActive { get; set; }
    public bool IsBookable { get; set; }
    public double? CoverageRadiusMiles { get; set; }
    public int? MaxTravelTimeMinutes { get; set; }
    public DateTime LastSyncedUtc { get; set; }
}
