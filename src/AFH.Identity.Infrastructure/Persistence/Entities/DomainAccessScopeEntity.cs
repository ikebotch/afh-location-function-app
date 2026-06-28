namespace AFH.Identity.Infrastructure.Persistence.Entities;

public sealed class DomainAccessScopeEntity
{
    public Guid Id { get; set; }
    public string Area { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string? ScopeValue { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
