using AFH.Identity.Application.Models;

namespace AFH.Identity.Application.Abstractions;

public interface IIdentityCurrentUserService
{
    Task<IdentityCurrentUserResult> GetCurrentUserAsync(
        string bearerToken,
        CancellationToken ct);
}
