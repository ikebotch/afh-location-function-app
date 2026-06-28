namespace AFH.Identity.Contracts.V1.Requests;

public sealed class IdentityUserAccessScopeMappingRequest
{
    public Guid? UserProfileId { get; init; }
    public Guid? AccessScopeId { get; init; }
    public string? ExternalSubject { get; init; }
    public string? Email { get; init; }
    public string? Area { get; init; }
    public string? ScopeType { get; init; }
    public string? ScopeValue { get; init; }
    public string? DisplayName { get; init; }
    public bool? IsEnabled { get; init; }
}
