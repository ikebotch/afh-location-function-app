namespace AFH.Location.Application.Models.Auth;

public sealed record DomainUserIdentity(
    string Email,
    IReadOnlyList<string> AppRoles,
    IReadOnlyList<string> Groups);
