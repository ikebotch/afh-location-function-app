namespace AFH.Adviser.Application.Models.Auth;

public sealed record DomainUserContext(
    string UserId,
    string ExternalSubject,
    string Email,
    string DisplayName,
    string? AdviserId,
    string? JobRole,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<DomainAccessScope> AccessScopes);

public sealed record DomainAccessScope(
    string Area,
    string ScopeType,
    string? ScopeValue = null,
    string? DisplayName = null);
