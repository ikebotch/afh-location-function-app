namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;

public sealed class DomainRolePermissionEntity
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
