namespace AFH.Adviser.Application.Models.Auth;

public sealed record DomainUserTokenValidationResult(
    bool IsValid,
    DomainUserIdentity? Identity,
    string? TenantId,
    string Code,
    string Message)
{
    public static DomainUserTokenValidationResult Success(
        DomainUserIdentity identity,
        string? tenantId) =>
        new(true, identity, tenantId, string.Empty, string.Empty);

    public static DomainUserTokenValidationResult Fail(
        string code,
        string message) =>
        new(false, null, null, code, message);
}
