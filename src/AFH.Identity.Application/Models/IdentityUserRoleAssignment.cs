namespace AFH.Identity.Application.Models;

public sealed class IdentityUserRoleAssignment
{
    public string Role { get; init; } = string.Empty;
    public Guid? UserProfileId { get; init; }
    public string? Email { get; init; }
    public string? ExternalRole { get; init; }
    public string? ExternalGroupId { get; init; }
    public bool IsEnabled { get; init; } = true;
}
