namespace AFH.Identity.Application.Models;

public sealed class IdentityCurrentUserResult
{
    public bool IsSuccess { get; init; }
    public IdentityCurrentUser? User { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }

    public static IdentityCurrentUserResult Success(IdentityCurrentUser user) =>
        new() { IsSuccess = true, User = user };

    public static IdentityCurrentUserResult Fail(string code, string message) =>
        new() { IsSuccess = false, FailureCode = code, FailureMessage = message };
}
