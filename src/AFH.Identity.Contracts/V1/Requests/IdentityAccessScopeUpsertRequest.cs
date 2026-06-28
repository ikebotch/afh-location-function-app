namespace AFH.Identity.Contracts.V1.Requests;

public sealed class IdentityAccessScopeUpsertRequest
{
    public Guid? AccessScopeId { get; init; }
    public string? Area { get; init; }
    public string? ScopeType { get; init; }
    public string? ScopeValue { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public bool? IsEnabled { get; init; }
}
