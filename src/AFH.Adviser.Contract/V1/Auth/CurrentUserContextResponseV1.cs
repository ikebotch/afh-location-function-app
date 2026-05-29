namespace AFH.Adviser.Contract.V1.Auth;

public sealed record CurrentUserContextResponseV1(
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
