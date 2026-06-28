using AFH.Identity.Application.Models;

namespace AFH.Identity.Application.Abstractions;

public interface IIdentityRbacAdminService
{
    Task<IReadOnlyList<IdentityUserProfileResult>> ListUserProfilesAsync(CancellationToken ct);

    Task<IdentityUserProfileResult?> GetUserProfileAsync(Guid userProfileId, CancellationToken ct);

    Task<IdentityUserProfileResult> UpsertUserProfileAsync(
        IdentityUserProfileUpsert upsert,
        CancellationToken ct);

    Task<bool> DeleteUserProfileAsync(Guid userProfileId, CancellationToken ct);

    Task<IReadOnlyList<IdentityPermissionResult>> ListPermissionsAsync(CancellationToken ct);

    Task<IdentityPermissionResult> UpsertPermissionAsync(
        IdentityPermissionUpsert upsert,
        CancellationToken ct);

    Task<bool> DeletePermissionAsync(Guid permissionId, CancellationToken ct);

    Task<IReadOnlyList<IdentityRoleAdminResult>> ListRolesAsync(CancellationToken ct);

    Task<IdentityRoleAdminResult?> GetRoleAsync(Guid roleId, CancellationToken ct);

    Task<IdentityRoleAdminResult> UpsertRoleAsync(
        string role,
        IReadOnlyList<string> permissions,
        CancellationToken ct);

    Task<IdentityRoleAdminResult> AddRolePermissionAsync(
        string role,
        string permission,
        CancellationToken ct);

    Task<bool> RemoveRolePermissionAsync(
        string role,
        string permission,
        CancellationToken ct);

    Task<bool> DeleteRoleAsync(Guid roleId, CancellationToken ct);

    Task<IReadOnlyList<IdentityUserRoleMappingResult>> ListUserRoleMappingsAsync(CancellationToken ct);

    Task<IdentityUserRoleMappingResult> AssignUserRoleAsync(
        IdentityUserRoleAssignment assignment,
        CancellationToken ct);

    Task<bool> DeleteUserRoleMappingAsync(Guid mappingId, CancellationToken ct);

    Task<IReadOnlyList<IdentityUserPermissionMappingResult>> ListUserPermissionMappingsAsync(CancellationToken ct);

    Task<IdentityUserPermissionMappingResult> AssignUserPermissionAsync(
        IdentityUserPermissionAssignment assignment,
        CancellationToken ct);

    Task<bool> DeleteUserPermissionMappingAsync(Guid mappingId, CancellationToken ct);

    Task<IReadOnlyList<IdentityAccessScopeAdminResult>> ListAccessScopesAsync(CancellationToken ct);

    Task<IdentityAccessScopeAdminResult> UpsertAccessScopeAsync(
        IdentityAccessScopeUpsert upsert,
        CancellationToken ct);

    Task<bool> DeleteAccessScopeAsync(Guid accessScopeId, CancellationToken ct);

    Task<IReadOnlyList<IdentityUserAccessScopeMappingResult>> ListUserAccessScopeMappingsAsync(CancellationToken ct);

    Task<IdentityUserAccessScopeMappingResult> AssignUserAccessScopeAsync(
        IdentityUserAccessScopeAssignment assignment,
        CancellationToken ct);

    Task<bool> DeleteUserAccessScopeMappingAsync(Guid mappingId, CancellationToken ct);
}
