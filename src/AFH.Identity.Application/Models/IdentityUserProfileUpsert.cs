namespace AFH.Identity.Application.Models;

public sealed class IdentityUserProfileUpsert
{
    public string? ExternalSubject { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? AdviserId { get; init; }
    public string? Status { get; init; }
}
