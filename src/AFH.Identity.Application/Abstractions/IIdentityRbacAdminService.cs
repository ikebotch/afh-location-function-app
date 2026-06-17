using AFH.Identity.Application.Models;

namespace AFH.Identity.Application.Abstractions;

public interface IIdentityRbacAdminService
{
    Task<IdentityUserProfileResult> UpsertUserProfileAsync(
        IdentityUserProfileUpsert upsert,
        CancellationToken ct);

    Task<IdentityRoleAdminResult> UpsertRoleAsync(
        string role,
        IReadOnlyList<string> permissions,
        CancellationToken ct);

    Task<IdentityRoleAdminResult> AddRolePermissionAsync(
        string role,
        string permission,
        CancellationToken ct);

    Task<IdentityUserRoleMappingResult> AssignUserRoleAsync(
        IdentityUserRoleAssignment assignment,
        CancellationToken ct);
}
