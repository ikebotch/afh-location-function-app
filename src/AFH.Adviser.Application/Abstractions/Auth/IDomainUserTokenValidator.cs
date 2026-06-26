using AFH.Adviser.Application.Models.Auth;

namespace AFH.Adviser.Application.Abstractions.Auth;

public interface IDomainUserTokenValidator
{
    Task<DomainUserTokenValidationResult> ValidateAsync(
        string? bearerToken,
        CancellationToken ct);
}
