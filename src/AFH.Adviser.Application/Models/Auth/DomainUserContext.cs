namespace AFH.Adviser.Application.Models.Auth;

public sealed record DomainUserContext(
    string UserId,
    string ExternalSubject,
    string Email,
    string DisplayName,
    string? AdviserId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
