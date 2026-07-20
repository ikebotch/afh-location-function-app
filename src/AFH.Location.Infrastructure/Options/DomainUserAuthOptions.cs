namespace AFH.Location.Infrastructure.Options;

public sealed class DomainUserAuthOptions
{
    public const string SectionName = "DomainUserAuth";

    public bool Enabled { get; set; }
    public string? TenantId { get; set; }
    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    public bool AllowMockTokens { get; set; }
    public string[] AllowedTenantIds { get; set; } = [];
    public string[] AllowedEmailDomains { get; set; } = [];
    public bool AllowExternalGuestsFromAllowedTenants { get; set; } = true;
}
