namespace AFH.Identity.Contracts.V1.Responses;

public sealed class IdentityAccessScopeAdminResponse
{
    public Guid AccessScopeId { get; init; }
    public string Area { get; init; } = string.Empty;
    public string ScopeType { get; init; } = string.Empty;
    public string? ScopeValue { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
}
