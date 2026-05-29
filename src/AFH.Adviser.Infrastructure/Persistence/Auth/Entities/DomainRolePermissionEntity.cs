namespace AFH.Adviser.Infrastructure.Persistence.Auth.Entities;

public sealed class DomainRolePermissionEntity
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
