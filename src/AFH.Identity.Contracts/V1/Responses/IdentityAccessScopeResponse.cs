namespace AFH.Identity.Contracts.V1.Responses;

public sealed class IdentityAccessScopeResponse
{
    public string Area { get; init; } = string.Empty;
    public string ScopeType { get; init; } = string.Empty;
    public string? ScopeValue { get; init; }
    public string? DisplayName { get; init; }
}
