using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Adviser.Application.Models.Auth;
using AFH.Identity.Application.Abstractions;
using AFH.Identity.Application.Models;
using AFH.Location.Infrastructure.Options;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

namespace AFH.Identity.Infrastructure.Services;

public sealed class LocationIdentityCurrentUserService : IIdentityCurrentUserService
{
    private readonly DomainUserAuthOptions _options;
    private readonly IDomainUserContextStore _contextStore;

    public LocationIdentityCurrentUserService(
        IOptions<DomainUserAuthOptions> options,
        IDomainUserContextStore contextStore)
    {
        _options = options.Value;
        _contextStore = contextStore;
    }

    public async Task<IdentityCurrentUserResult> GetCurrentUserAsync(
        string bearerToken,
        CancellationToken ct)
    {
        if (!_options.Enabled)
            return IdentityCurrentUserResult.Fail("AUTH_DISABLED", "DomainUserAuth is not enabled.");

        var identityResult = TryReadIdentity(bearerToken);
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

    private (DomainUserIdentity? Identity, string? TenantId, string Code, string Message) TryReadIdentity(string? bearerToken)
    {
        var token = NormalizeBearerToken(bearerToken);
        if (string.IsNullOrWhiteSpace(token))
            return (null, null, "AUTH_ERROR", "Missing user bearer token.");

        JwtSecurityToken jwt;
        try
        {
            jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        }
        catch
        {
            return (null, null, "AUTH_ERROR", "Bearer token is invalid.");
        }

        if (!IsAllowedAudience(jwt))
            return (null, null, "FORBIDDEN", "Bearer token audience is not allowed.");

        var tenantId = FirstClaim(jwt, "tid", "tenantid");
        if (!IsAllowedTenant(jwt, tenantId))
            return (null, tenantId, "FORBIDDEN", "Bearer token tenant is not allowed.");

        var email = FirstClaim(jwt, "preferred_username", "upn", "email");
        if (string.IsNullOrWhiteSpace(email))
            return (null, tenantId, "FORBIDDEN", "Bearer token does not contain a user email.");

        if (!IsAllowedEmailDomain(email))
            return (null, tenantId, "FORBIDDEN", "User email domain is not allowed.");

        var roles = jwt.Claims
            .Where(x => x.Type is "roles" or "role" or "app_role")
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var groups = jwt.Claims
            .Where(x => x.Type == "groups")
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var userId = FirstClaim(jwt, "oid", "sub") ?? email.Trim();
        var displayName = FirstClaim(jwt, "name") ?? email.Trim();

        return (new DomainUserIdentity(userId, email.Trim(), displayName, roles, groups), tenantId, string.Empty, string.Empty);
    }

    private bool IsAllowedAudience(JwtSecurityToken jwt) =>
        string.IsNullOrWhiteSpace(_options.Audience)
        || jwt.Audiences.Contains(_options.Audience.Trim(), StringComparer.OrdinalIgnoreCase);

    private bool IsAllowedTenant(JwtSecurityToken jwt, string? tenantId)
    {
        var allowedTenants = _options.AllowedTenantIds
            .Append(_options.TenantId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedTenants.Length == 0)
            return true;

        if (!string.IsNullOrWhiteSpace(tenantId) && allowedTenants.Contains(tenantId.Trim(), StringComparer.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(jwt.Issuer)
            && allowedTenants.Any(tenant => jwt.Issuer.Contains(tenant, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsAllowedEmailDomain(string email)
    {
        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1)
            return false;

        var allowed = _options.AllowedEmailDomains
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().TrimStart('@'))
            .ToArray();

        return allowed.Length == 0
            || allowed.Any(domain => email.EndsWith("@" + domain, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FirstClaim(JwtSecurityToken jwt, params string[] types) =>
        types
            .Select(type => jwt.Claims.FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase))?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? NormalizeBearerToken(string? bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
            return null;

        var trimmed = bearerToken.Trim();
        return trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? trimmed["Bearer ".Length..].Trim()
            : trimmed;
    }
}
