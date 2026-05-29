namespace AFH.Adviser.Application.Models.Auth;

public sealed record DomainUserContext(
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
