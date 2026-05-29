namespace AFH.Adviser.Application.Models.Auth;

public sealed record DomainUserIdentity(
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> AppRoles,
    IReadOnlyList<string> Groups);
