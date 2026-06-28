namespace AFH.Identity.Application.Models;

public sealed class IdentityUserAccessScopeAssignment
{
    public Guid? UserProfileId { get; init; }
    public Guid? AccessScopeId { get; init; }
    public string? ExternalSubject { get; init; }
    public string? Email { get; init; }
    public string Area { get; init; } = string.Empty;
    public string ScopeType { get; init; } = string.Empty;
    public string? ScopeValue { get; init; }
    public string? DisplayName { get; init; }
    public bool IsEnabled { get; init; } = true;
}
