namespace AFH.Adviser.Infrastructure.Persistence.Auth.Entities;

public sealed class DomainUserRoleMappingEntity
{
    public Guid Id { get; set; }
    public Guid? UserProfileId { get; set; }
    public Guid RoleId { get; set; }
    public string? Email { get; set; }
    public string? ExternalRole { get; set; }
    public string? ExternalGroupId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
