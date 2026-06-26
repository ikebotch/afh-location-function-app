using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Adviser.Application.Models.Auth;
using AFH.Location.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AFH.Identity.Infrastructure.Services;

public sealed class EntraDomainUserTokenValidator : IDomainUserTokenValidator
{
    private readonly DomainUserAuthOptions _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public EntraDomainUserTokenValidator(IOptions<DomainUserAuthOptions> options)
        : this(options, CreateConfigurationManager(options.Value))
    {
    }

    internal EntraDomainUserTokenValidator(
        IOptions<DomainUserAuthOptions> options,
        IConfigurationManager<OpenIdConnectConfiguration> configurationManager)
    {
        _options = options.Value;
        _configurationManager = configurationManager;
    }

    public async Task<DomainUserTokenValidationResult> ValidateAsync(
        string? bearerToken,
        CancellationToken ct)
    {
        if (!_options.Enabled)
            return DomainUserTokenValidationResult.Fail("AUTH_DISABLED", "DomainUserAuth is not enabled.");

        var token = NormalizeBearerToken(bearerToken);
        if (string.IsNullOrWhiteSpace(token))
            return DomainUserTokenValidationResult.Fail("AUTH_ERROR", "Missing bearer token.");

        if (string.IsNullOrWhiteSpace(_options.Audience))
            return DomainUserTokenValidationResult.Fail("AUTH_ERROR", "DomainUserAuth audience is not configured.");

        try
        {
            var configuration = await _configurationManager.GetConfigurationAsync(ct);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidAudiences = [_options.Audience],
                ValidIssuers = configuration.Issuer is null ? null : [configuration.Issuer],
                IssuerSigningKeys = configuration.SigningKeys,
                NameClaimType = "name",
                RoleClaimType = "roles",
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
            var tenantId = GetClaimValue(principal, "tid", "http://schemas.microsoft.com/identity/claims/tenantid");
            if (!IsAllowedTenant(tenantId))
                return DomainUserTokenValidationResult.Fail("FORBIDDEN", "Bearer token tenant is not allowed.");

            var email = GetClaimValue(principal, ClaimTypes.Upn, "preferred_username", "upn", ClaimTypes.Email, "email");
            if (string.IsNullOrWhiteSpace(email))
                return DomainUserTokenValidationResult.Fail("FORBIDDEN", "Bearer token does not contain a user email.");

            if (!IsAllowedEmailDomain(email))
                return DomainUserTokenValidationResult.Fail("FORBIDDEN", "User email domain is not allowed.");

            var roles = principal.Claims
                .Where(x => x.Type is "roles" or "role" or "app_role")
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var groups = principal.Claims
                .Where(x => x.Type == "groups")
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var userId = GetClaimValue(principal, "oid", ClaimTypes.NameIdentifier, "sub") ?? email.Trim();
            var displayName = GetClaimValue(principal, "name", ClaimTypes.Name) ?? email.Trim();

            return DomainUserTokenValidationResult.Success(
                new DomainUserIdentity(userId, email.Trim(), displayName, roles, groups),
                tenantId);
        }
        catch (SecurityTokenException ex)
        {
            return DomainUserTokenValidationResult.Fail("AUTH_ERROR", ex.Message);
        }
        catch (Exception ex)
        {
            return DomainUserTokenValidationResult.Fail("AUTH_ERROR", ex.Message);
        }
    }

    private bool IsAllowedTenant(string? tenantId)
    {
        var allowedTenants = _options.AllowedTenantIds
            .Append(_options.TenantId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return allowedTenants.Length == 0
            || (!string.IsNullOrWhiteSpace(tenantId) && allowedTenants.Contains(tenantId.Trim(), StringComparer.OrdinalIgnoreCase));
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

    private static string GetAuthority(DomainUserAuthOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Authority))
            return options.Authority;

        if (string.IsNullOrWhiteSpace(options.TenantId))
            throw new InvalidOperationException($"{DomainUserAuthOptions.SectionName}:TenantId is required.");

        return $"https://login.microsoftonline.com/{options.TenantId}/v2.0";
    }

    private static IConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(DomainUserAuthOptions options)
    {
        var metadataAddress = $"{GetAuthority(options).TrimEnd('/')}/.well-known/openid-configuration";
        return new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = options.RequireHttpsMetadata });
    }

    private static string? GetClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var claim = principal.FindFirst(claimType);
            if (!string.IsNullOrWhiteSpace(claim?.Value))
                return claim.Value;
        }

        return null;
    }

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
