namespace AFH.Adviser.Infrastructure.Persistence.Auth.Entities;

public sealed class DomainUserProfileEntity
{
    public Guid Id { get; set; }
    public string ExternalSubject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AdviserId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
