namespace AFH.Identity.Application.Models;

public sealed class IdentityAccessScopeUpsert
{
    public Guid? AccessScopeId { get; init; }
    public string Area { get; init; } = string.Empty;
    public string ScopeType { get; init; } = string.Empty;
    public string? ScopeValue { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
}
