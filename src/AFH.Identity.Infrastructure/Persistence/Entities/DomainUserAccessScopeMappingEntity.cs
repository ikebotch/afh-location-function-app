namespace AFH.Identity.Infrastructure.Persistence.Entities;

public sealed class DomainUserAccessScopeMappingEntity
{
    public Guid Id { get; set; }
    public Guid? UserProfileId { get; set; }
    public Guid AccessScopeId { get; set; }
    public string? ExternalSubject { get; set; }
    public string? Email { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
