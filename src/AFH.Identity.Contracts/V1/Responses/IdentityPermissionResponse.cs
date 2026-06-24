namespace AFH.Identity.Contracts.V1.Responses;

public sealed class IdentityPermissionResponse
{
    public Guid PermissionId { get; init; }
    public string Permission { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
}
