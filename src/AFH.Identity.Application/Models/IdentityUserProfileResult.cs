namespace AFH.Identity.Application.Models;

public sealed class IdentityUserProfileResult
{
    public Guid UserProfileId { get; init; }
    public string ExternalSubject { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AdviserId { get; init; }
    public string Status { get; init; } = string.Empty;
}
