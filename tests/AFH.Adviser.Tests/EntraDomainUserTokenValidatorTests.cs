using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AFH.Identity.Infrastructure.Services;
using AFH.Location.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AFH.Adviser.Tests;

public sealed class EntraDomainUserTokenValidatorTests
{
    private const string TenantId = "test-tenant";
    private const string V2Issuer = "https://login.microsoftonline.com/test-tenant/v2.0";
    private const string V1Issuer = "https://sts.windows.net/test-tenant/";
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"));

    [Fact]
    public async Task ValidateAsync_AcceptsEntraV1IssuerForConfiguredTenant()
    {
        var validator = CreateValidator(new DomainUserAuthOptions
        {
            Enabled = true,
            TenantId = TenantId,
            Audience = "api://booking-api",
            AllowedTenantIds = [TenantId],
            AllowedEmailDomains = ["afh.co.uk"]
        });

        var result = await validator.ValidateAsync(
            CreateToken("api://booking-api", TenantId, "alex@afh.co.uk", V1Issuer),
            CancellationToken.None);

        Assert.NotNull(result.Identity);
        Assert.Equal("alex@afh.co.uk", result.Identity!.Email);
        Assert.Equal(TenantId, result.TenantId);
    }

    [Fact]
    public async Task ValidateAsync_RejectsV1IssuerForUnconfiguredTenant()
    {
        var validator = CreateValidator(new DomainUserAuthOptions
        {
            Enabled = true,
            TenantId = TenantId,
            Audience = "api://booking-api",
            AllowedTenantIds = [TenantId],
            AllowedEmailDomains = ["afh.co.uk"]
        });

        var result = await validator.ValidateAsync(
            CreateToken("api://booking-api", "other-tenant", "alex@afh.co.uk", "https://sts.windows.net/other-tenant/"),
            CancellationToken.None);

        Assert.Null(result.Identity);
        Assert.Equal("AUTH_ERROR", result.Code);
    }

    [Fact]
    public async Task ValidateAsync_AcceptsExternalGuestEmailWhenTokenTenantIsAllowed()
    {
        var validator = CreateValidator(new DomainUserAuthOptions
        {
            Enabled = true,
            TenantId = TenantId,
            Audience = "api://booking-api",
            AllowedTenantIds = [TenantId],
            AllowedEmailDomains = ["internal.com"]
        });

        var result = await validator.ValidateAsync(
            CreateToken("api://booking-api", TenantId, "alex@external.com", V2Issuer),
            CancellationToken.None);

        Assert.NotNull(result.Identity);
        Assert.Equal("alex@external.com", result.Identity!.Email);
        Assert.Equal(TenantId, result.TenantId);
    }

    private static EntraDomainUserTokenValidator CreateValidator(DomainUserAuthOptions options)
    {
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = V2Issuer
        };
        configuration.SigningKeys.Add(SigningKey);

        return new EntraDomainUserTokenValidator(
            Options.Create(options),
            new StaticOpenIdConfigurationManager(configuration));
    }

    private static string CreateToken(
        string audience,
        string tenantId,
        string email,
        string issuer)
    {
        var claims = new List<Claim>
        {
            new("tid", tenantId),
            new("oid", "user-123"),
            new("name", "Alex Example"),
            new("preferred_username", email)
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Audience = audience,
            Issuer = issuer,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        };

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
    }

    private sealed class StaticOpenIdConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private readonly OpenIdConnectConfiguration _configuration;

        public StaticOpenIdConfigurationManager(OpenIdConnectConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
            => Task.FromResult(_configuration);

        public void RequestRefresh()
        {
        }
    }
}
