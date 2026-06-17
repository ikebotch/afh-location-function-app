namespace AFH.Adviser.Infrastructure.Persistence.Auth.Entities;

public sealed class DomainPermissionEntity
{
    public Guid Id { get; set; }
    public string Permission { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
