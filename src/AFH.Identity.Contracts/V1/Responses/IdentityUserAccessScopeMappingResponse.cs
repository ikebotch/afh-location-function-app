namespace AFH.Identity.Contracts.V1.Responses;

public sealed class IdentityUserAccessScopeMappingResponse
{
    public Guid MappingId { get; init; }
    public Guid? UserProfileId { get; init; }
    public Guid AccessScopeId { get; init; }
    public string? ExternalSubject { get; init; }
    public string? Email { get; init; }
    public string Area { get; init; } = string.Empty;
    public string ScopeType { get; init; } = string.Empty;
    public string? ScopeValue { get; init; }
    public string? DisplayName { get; init; }
    public bool IsEnabled { get; init; }
}
