using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Identity.Application.Abstractions;
using AFH.Identity.Application.Models;

namespace AFH.Identity.Infrastructure.Services;

public sealed class LocationIdentityCurrentUserService : IIdentityCurrentUserService
{
    private readonly IDomainUserTokenValidator _tokenValidator;
    private readonly IDomainUserContextStore _contextStore;

    public LocationIdentityCurrentUserService(
        IDomainUserTokenValidator tokenValidator,
        IDomainUserContextStore contextStore)
    {
        _tokenValidator = tokenValidator;
        _contextStore = contextStore;
    }

    public async Task<IdentityCurrentUserResult> GetCurrentUserAsync(
        string bearerToken,
        CancellationToken ct)
    {
        var identityResult = await _tokenValidator.ValidateAsync(bearerToken, ct);
        if (identityResult.Identity is null)
            return IdentityCurrentUserResult.Fail(identityResult.Code, identityResult.Message);

        var context = await _contextStore.GetContextAsync(identityResult.Identity, ct);
        if (context.Roles.Count == 0)
            return IdentityCurrentUserResult.Fail("FORBIDDEN", "Signed-in user does not have a mapped Location domain role.");

        return IdentityCurrentUserResult.Success(new IdentityCurrentUser
        {
            UserId = context.UserId,
            ExternalSubject = context.ExternalSubject,
            Email = context.Email,
            DisplayName = context.DisplayName,
            AdviserId = context.AdviserId,
            JobRole = context.JobRole,
            TenantId = identityResult.TenantId,
            Roles = context.Roles,
            Permissions = context.Permissions
        });
    }
}
