namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;

public sealed class DomainRoleEntity
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
