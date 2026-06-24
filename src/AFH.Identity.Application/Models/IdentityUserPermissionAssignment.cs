namespace AFH.Identity.Application.Models;

public sealed class IdentityUserPermissionAssignment
{
    public string Permission { get; init; } = string.Empty;
    public Guid? UserProfileId { get; init; }
    public string? ExternalSubject { get; init; }
    public string? Email { get; init; }
    public bool IsGranted { get; init; } = true;
    public bool IsEnabled { get; init; } = true;
    public string? Reason { get; init; }
}
