namespace AFH.Identity.Contracts.V1.Requests;

public sealed class IdentityUserRoleMappingRequest
{
    public string? Role { get; init; }
    public string? Email { get; init; }
    public string? ExternalRole { get; init; }
    public string? ExternalGroupId { get; init; }
    public bool? IsEnabled { get; init; }
}
