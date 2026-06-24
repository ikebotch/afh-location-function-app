namespace AFH.Identity.Application.Models;

public sealed class IdentityCurrentUser
{
    public string UserId { get; init; } = string.Empty;
    public string ExternalSubject { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AdviserId { get; init; }
    public string? JobRole { get; init; }
    public string? TenantId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
