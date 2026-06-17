namespace AFH.Identity.Contracts.V1.Responses;

public sealed class IdentityUserRoleMappingResponse
{
    public Guid MappingId { get; init; }
    public Guid? UserProfileId { get; init; }
    public Guid RoleId { get; init; }
    public string Role { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? ExternalRole { get; init; }
    public string? ExternalGroupId { get; init; }
    public bool IsEnabled { get; init; }
}
