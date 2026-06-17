namespace AFH.Identity.Application.Models;

public sealed class IdentityUserRoleMappingResult
{
    public Guid MappingId { get; init; }
    public Guid RoleId { get; init; }
    public string Role { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? ExternalRole { get; init; }
    public string? ExternalGroupId { get; init; }
    public bool IsEnabled { get; init; }
}
