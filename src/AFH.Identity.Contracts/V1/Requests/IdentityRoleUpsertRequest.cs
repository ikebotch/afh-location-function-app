namespace AFH.Identity.Contracts.V1.Requests;

public sealed class IdentityRoleUpsertRequest
{
    public string? Role { get; init; }
    public IReadOnlyList<string>? Permissions { get; init; }
}
