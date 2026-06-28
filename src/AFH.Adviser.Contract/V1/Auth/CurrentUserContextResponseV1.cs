namespace AFH.Adviser.Contract.V1.Auth;

public sealed record CurrentUserContextResponseV1(
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<CurrentUserAccessScopeResponseV1> AccessScopes);

public sealed record CurrentUserAccessScopeResponseV1(
    string Area,
    string ScopeType,
    string? ScopeValue = null,
    string? DisplayName = null);
