namespace AFH.Identity.Application.Models;

public sealed class IdentityUserPermissionMappingResult
{
    public Guid MappingId { get; init; }
    public string Permission { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? ExternalRole { get; init; }
    public string? ExternalGroupId { get; init; }
    public bool IsEnabled { get; init; }
}
