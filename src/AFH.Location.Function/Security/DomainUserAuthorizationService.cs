using System.IdentityModel.Tokens.Jwt;
using System.Net;
using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Adviser.Application.Models.Auth;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Middleware;
using AFH.Location.Infrastructure.Options;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;

namespace AFH.Location.Function.Security;

public sealed class DomainUserAuthorizationService : IDomainUserAuthorizationService
{
    private readonly DomainUserAuthOptions _domainOptions;
    private readonly InternalApiAuthOptions _internalOptions;
    private readonly IDomainUserPermissionStore _permissions;

    public DomainUserAuthorizationService(
        IOptions<DomainUserAuthOptions> domainOptions,
        IOptions<InternalApiAuthOptions> internalOptions,
        IDomainUserPermissionStore permissions)
    {
        _domainOptions = domainOptions.Value;
        _internalOptions = internalOptions.Value;
        _permissions = permissions;
    }

    public async Task<HttpResponseData?> AuthorizeAsync(
        HttpRequestData req,
        string permission,
        bool allowInternal,
        CancellationToken ct)
    {
        var authorization = GetAuthorizationHeader(req);

        if (allowInternal && InternalApiAuthMiddleware.ValidateAuthorization(_internalOptions.Token, authorization) is null)
            return null;

        if (!_domainOptions.Enabled)
            return await req.WriteFailureAsync(HttpStatusCode.Forbidden, new { code = "AUTH_DISABLED", message = "DomainUserAuth is not enabled." }, ct);

        var identityResult = TryReadIdentity(authorization);
        if (identityResult.Identity is null)
            return await req.WriteFailureAsync(identityResult.StatusCode, new { code = "AUTH_ERROR", message = identityResult.Message }, ct);

        var allowed = await _permissions.HasPermissionAsync(identityResult.Identity, permission, ct);
        return allowed
            ? null
            : await req.WriteFailureAsync(HttpStatusCode.Forbidden, new { code = "FORBIDDEN", message = $"Permission '{permission}' is required." }, ct);
    }

    private (DomainUserIdentity? Identity, HttpStatusCode StatusCode, string Message) TryReadIdentity(string? authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization))
            return (null, HttpStatusCode.Unauthorized, "Missing Authorization header.");

        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return (null, HttpStatusCode.Unauthorized, "Invalid Authorization header.");

        var token = authorization["Bearer ".Length..].Trim();
        JwtSecurityToken jwt;
        try
        {
            jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        }
        catch
        {
            return (null, HttpStatusCode.Unauthorized, "Bearer token is invalid.");
        }

        if (!IsAllowedAudience(jwt))
            return (null, HttpStatusCode.Forbidden, "Bearer token audience is not allowed.");

        if (!IsAllowedTenant(jwt))
            return (null, HttpStatusCode.Forbidden, "Bearer token tenant is not allowed.");

        var email = FirstClaim(jwt, "preferred_username", "upn", "email");
        if (string.IsNullOrWhiteSpace(email))
            return (null, HttpStatusCode.Forbidden, "Bearer token does not contain a user email.");

        if (!IsAllowedEmailDomain(email))
            return (null, HttpStatusCode.Forbidden, "User email domain is not allowed.");

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

        return (new DomainUserIdentity(email.Trim(), roles, groups), HttpStatusCode.OK, string.Empty);
    }

    private bool IsAllowedAudience(JwtSecurityToken jwt)
        => string.IsNullOrWhiteSpace(_domainOptions.Audience)
           || jwt.Audiences.Contains(_domainOptions.Audience.Trim(), StringComparer.OrdinalIgnoreCase);

    private bool IsAllowedTenant(JwtSecurityToken jwt)
    {
        var tenantId = FirstClaim(jwt, "tid", "tenantid");
        var allowedTenants = _domainOptions.AllowedTenantIds
            .Append(_domainOptions.TenantId)
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

        var allowed = _domainOptions.AllowedEmailDomains
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().TrimStart('@'))
            .ToArray();

        return allowed.Length == 0
               || allowed.Any(domain => email.EndsWith("@" + domain, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FirstClaim(JwtSecurityToken jwt, params string[] types)
        => types
            .Select(type => jwt.Claims.FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase))?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? GetAuthorizationHeader(HttpRequestData req)
        => req.Headers.TryGetValues("Authorization", out var values) ? values.FirstOrDefault() : null;
}
