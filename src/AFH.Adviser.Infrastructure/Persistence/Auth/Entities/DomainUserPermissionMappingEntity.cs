namespace AFH.Adviser.Infrastructure.Persistence.Auth.Entities;

public sealed class DomainUserPermissionMappingEntity
{
    public Guid Id { get; set; }
    public Guid PermissionId { get; set; }
    public string? Email { get; set; }
    public string? ExternalRole { get; set; }
    public string? ExternalGroupId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
