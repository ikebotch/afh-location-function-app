namespace AFH.Identity.Infrastructure.Persistence.Entities;

public sealed class DomainUserPermissionMappingEntity
{
    public Guid Id { get; set; }
    public Guid? UserProfileId { get; set; }
    public Guid PermissionId { get; set; }
    public string? ExternalSubject { get; set; }
    public string? Email { get; set; }
    public bool IsGranted { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public string? Reason { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
