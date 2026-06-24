namespace AFH.Identity.Application.Models;

public sealed class IdentityPermissionUpsert
{
    public string Permission { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public bool IsEnabled { get; init; } = true;
}
