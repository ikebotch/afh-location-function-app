namespace AFH.Identity.Application.Models;

public sealed class IdentityRoleAdminResult
{
    public Guid RoleId { get; init; }
    public string Role { get; init; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
