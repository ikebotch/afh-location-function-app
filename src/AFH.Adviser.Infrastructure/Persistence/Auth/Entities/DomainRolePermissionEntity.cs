namespace AFH.Adviser.Infrastructure.Persistence.Auth.Entities;

public sealed class DomainRolePermissionEntity
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public DateTime CreatedUtc { get; set; }
}
