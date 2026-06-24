namespace AFH.Identity.Contracts.V1.Requests;

public sealed class IdentityPermissionUpsertRequest
{
    public string? Permission { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public bool? IsEnabled { get; init; }
}
