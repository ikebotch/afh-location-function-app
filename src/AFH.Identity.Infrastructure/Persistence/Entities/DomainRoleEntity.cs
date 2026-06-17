namespace AFH.Identity.Infrastructure.Persistence.Entities;

public sealed class DomainRoleEntity
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
