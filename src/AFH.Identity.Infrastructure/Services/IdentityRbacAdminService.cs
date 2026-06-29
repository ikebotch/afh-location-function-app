using AFH.Identity.Application.Abstractions;
using AFH.Identity.Application.Models;
using AFH.Identity.Infrastructure.Persistence;
using AFH.Identity.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Identity.Infrastructure.Services;

public sealed class IdentityRbacAdminService : IIdentityRbacAdminService
{
    private readonly IdentityDbContext _db;

    public IdentityRbacAdminService(IdentityDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<IdentityUserProfileResult>> ListUserProfilesAsync(CancellationToken ct) =>
        await _db.DomainUserProfiles
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Select(x => ToUserProfileResult(x))
            .ToArrayAsync(ct);

    public async Task<IdentityUserProfileResult?> GetUserProfileAsync(Guid userProfileId, CancellationToken ct) =>
        await _db.DomainUserProfiles
            .AsNoTracking()
            .Where(x => x.Id == userProfileId)
            .Select(x => ToUserProfileResult(x))
            .SingleOrDefaultAsync(ct);

    public async Task<IdentityUserProfileResult> UpsertUserProfileAsync(
        IdentityUserProfileUpsert upsert,
        CancellationToken ct)
    {
        var normalizedEmail = NormalizeRequired(upsert.Email);
        var normalizedExternalSubject = NormalizeOptional(upsert.ExternalSubject) ?? normalizedEmail;
        var normalizedDisplayName = NormalizeOptional(upsert.DisplayName) ?? normalizedEmail;
        var normalizedAdviserId = NormalizeOptional(upsert.AdviserId);
        var normalizedJobRole = NormalizeOptional(upsert.JobRole);
        var normalizedStatus = NormalizeOptional(upsert.Status) ?? "Active";

        var profile = upsert.UserProfileId is Guid userProfileId
            ? await _db.DomainUserProfiles.SingleOrDefaultAsync(x => x.Id == userProfileId, ct)
            : null;

        profile ??= await _db.DomainUserProfiles
            .SingleOrDefaultAsync(x => x.ExternalSubject == normalizedExternalSubject, ct)
            ?? await _db.DomainUserProfiles.SingleOrDefaultAsync(x => x.Email == normalizedEmail, ct);

        var now = DateTime.UtcNow;
        if (profile is null)
        {
            profile = new DomainUserProfileEntity
            {
                Id = Guid.NewGuid(),
                ExternalSubject = normalizedExternalSubject,
                Email = normalizedEmail,
                DisplayName = normalizedDisplayName,
                AdviserId = normalizedAdviserId,
                JobRole = normalizedJobRole,
                Status = normalizedStatus,
                CreatedUtc = now
            };
            _db.DomainUserProfiles.Add(profile);
        }
        else
        {
            profile.ExternalSubject = normalizedExternalSubject;
            profile.Email = normalizedEmail;
            profile.DisplayName = normalizedDisplayName;
            profile.AdviserId = normalizedAdviserId;
            profile.JobRole = normalizedJobRole;
            profile.Status = normalizedStatus;
            profile.UpdatedUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return ToUserProfileResult(profile);
    }

    public async Task<bool> DeleteUserProfileAsync(Guid userProfileId, CancellationToken ct)
    {
        var profile = await _db.DomainUserProfiles.SingleOrDefaultAsync(x => x.Id == userProfileId, ct);
        if (profile is null)
            return false;

        var roleMappings = await _db.DomainUserRoleMappings.Where(x => x.UserProfileId == userProfileId).ToArrayAsync(ct);
        var permissionMappings = await _db.DomainUserPermissionMappings.Where(x => x.UserProfileId == userProfileId).ToArrayAsync(ct);
        var accessScopeMappings = await _db.DomainUserAccessScopeMappings.Where(x => x.UserProfileId == userProfileId).ToArrayAsync(ct);
        _db.DomainUserRoleMappings.RemoveRange(roleMappings);
        _db.DomainUserPermissionMappings.RemoveRange(permissionMappings);
        _db.DomainUserAccessScopeMappings.RemoveRange(accessScopeMappings);
        _db.DomainUserProfiles.Remove(profile);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<IdentityPermissionResult>> ListPermissionsAsync(CancellationToken ct) =>
        await _db.DomainPermissions
            .AsNoTracking()
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Permission)
            .Select(x => ToPermissionResult(x))
            .ToArrayAsync(ct);

    public async Task<IdentityPermissionResult> UpsertPermissionAsync(
        IdentityPermissionUpsert upsert,
        CancellationToken ct)
    {
        var permission = await GetOrCreatePermissionAsync(upsert.Permission, ct);
        permission.DisplayName = NormalizeOptional(upsert.DisplayName) ?? ToDisplayName(permission.Permission);
        permission.Description = NormalizeOptional(upsert.Description);
        permission.Category = NormalizeOptional(upsert.Category) ?? ToCategory(permission.Permission);
        permission.IsEnabled = upsert.IsEnabled;
        permission.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToPermissionResult(permission);
    }

    public async Task<bool> DeletePermissionAsync(Guid permissionId, CancellationToken ct)
    {
        var permission = await _db.DomainPermissions.SingleOrDefaultAsync(x => x.Id == permissionId, ct);
        if (permission is null)
            return false;

        var roleMappings = await _db.DomainRolePermissions.Where(x => x.PermissionId == permissionId).ToArrayAsync(ct);
        var userMappings = await _db.DomainUserPermissionMappings.Where(x => x.PermissionId == permissionId).ToArrayAsync(ct);
        _db.DomainRolePermissions.RemoveRange(roleMappings);
        _db.DomainUserPermissionMappings.RemoveRange(userMappings);
        _db.DomainPermissions.Remove(permission);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<IdentityRoleAdminResult>> ListRolesAsync(CancellationToken ct)
    {
        var roleIds = await _db.DomainRoles
            .AsNoTracking()
            .OrderBy(x => x.Role)
            .Select(x => x.Id)
            .ToArrayAsync(ct);

        var results = new List<IdentityRoleAdminResult>(roleIds.Length);
        foreach (var roleId in roleIds)
        {
            results.Add(await GetRoleResultAsync(roleId, ct));
        }

        return results;
    }

    public async Task<IdentityRoleAdminResult?> GetRoleAsync(Guid roleId, CancellationToken ct) =>
        await _db.DomainRoles.AsNoTracking().AnyAsync(x => x.Id == roleId, ct)
            ? await GetRoleResultAsync(roleId, ct)
            : null;

    public async Task<IdentityRoleAdminResult> UpsertRoleAsync(
        string role,
        IReadOnlyList<string> permissions,
        CancellationToken ct)
    {
        var entity = await GetOrCreateRoleAsync(role, ct);
        await UpsertRolePermissionsAsync(entity.Id, permissions, ct);
        await _db.SaveChangesAsync(ct);
        return await GetRoleResultAsync(entity.Id, ct);
    }

    public async Task<IdentityRoleAdminResult> AddRolePermissionAsync(
        string role,
        string permission,
        CancellationToken ct)
    {
        var entity = await GetOrCreateRoleAsync(role, ct);
        await UpsertRolePermissionsAsync(entity.Id, [permission], ct);
        await _db.SaveChangesAsync(ct);
        return await GetRoleResultAsync(entity.Id, ct);
    }

    public async Task<bool> RemoveRolePermissionAsync(string role, string permission, CancellationToken ct)
    {
        var normalizedRole = NormalizeRequired(role);
        var normalizedPermission = NormalizeRequired(permission);
        var mapping = await _db.DomainRolePermissions
            .Join(
                _db.DomainRoles.Where(x => x.Role == normalizedRole),
                rolePermission => rolePermission.RoleId,
                roleEntity => roleEntity.Id,
                (rolePermission, _) => rolePermission)
            .Join(
                _db.DomainPermissions.Where(x => x.Permission == normalizedPermission),
                rolePermission => rolePermission.PermissionId,
                permissionEntity => permissionEntity.Id,
                (rolePermission, _) => rolePermission)
            .SingleOrDefaultAsync(ct);

        if (mapping is null)
            return false;

        _db.DomainRolePermissions.Remove(mapping);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteRoleAsync(Guid roleId, CancellationToken ct)
    {
        var role = await _db.DomainRoles.SingleOrDefaultAsync(x => x.Id == roleId, ct);
        if (role is null)
            return false;

        var permissions = await _db.DomainRolePermissions.Where(x => x.RoleId == roleId).ToArrayAsync(ct);
        var userMappings = await _db.DomainUserRoleMappings.Where(x => x.RoleId == roleId).ToArrayAsync(ct);
        _db.DomainRolePermissions.RemoveRange(permissions);
        _db.DomainUserRoleMappings.RemoveRange(userMappings);
        _db.DomainRoles.Remove(role);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<IdentityUserRoleMappingResult>> ListUserRoleMappingsAsync(CancellationToken ct) =>
        await _db.DomainUserRoleMappings
            .AsNoTracking()
            .Join(
                _db.DomainRoles.AsNoTracking(),
                mapping => mapping.RoleId,
                role => role.Id,
                (mapping, role) => new IdentityUserRoleMappingResult
                {
                    MappingId = mapping.Id,
                    UserProfileId = mapping.UserProfileId,
                    RoleId = role.Id,
                    Role = role.Role,
                    Email = mapping.Email,
                    ExternalRole = mapping.ExternalRole,
                    ExternalGroupId = mapping.ExternalGroupId,
                    IsEnabled = mapping.IsEnabled
                })
            .OrderBy(x => x.Role)
            .ThenBy(x => x.Email)
            .ToArrayAsync(ct);

    public async Task<IdentityUserRoleMappingResult> AssignUserRoleAsync(
        IdentityUserRoleAssignment assignment,
        CancellationToken ct)
    {
        var role = await GetOrCreateRoleAsync(assignment.Role, ct);
        var normalizedEmail = NormalizeOptional(assignment.Email);
        var normalizedExternalRole = NormalizeOptional(assignment.ExternalRole);
        var normalizedExternalGroupId = NormalizeOptional(assignment.ExternalGroupId);
        var userProfileId = await ResolveUserProfileIdAsync(assignment.UserProfileId, normalizedEmail, ct);

        var mapping = await _db.DomainUserRoleMappings
            .SingleOrDefaultAsync(x =>
                x.RoleId == role.Id
                && x.UserProfileId == userProfileId
                && x.Email == normalizedEmail
                && x.ExternalRole == normalizedExternalRole
                && x.ExternalGroupId == normalizedExternalGroupId,
                ct);

        var now = DateTime.UtcNow;
        if (mapping is null)
        {
            mapping = new DomainUserRoleMappingEntity
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                UserProfileId = userProfileId,
                Email = normalizedEmail,
                ExternalRole = normalizedExternalRole,
                ExternalGroupId = normalizedExternalGroupId,
                IsEnabled = assignment.IsEnabled,
                CreatedUtc = now
            };
            _db.DomainUserRoleMappings.Add(mapping);
        }
        else
        {
            mapping.IsEnabled = assignment.IsEnabled;
            mapping.UpdatedUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return ToUserRoleMappingResult(mapping, role);
    }

    public async Task<bool> DeleteUserRoleMappingAsync(Guid mappingId, CancellationToken ct)
    {
        var mapping = await _db.DomainUserRoleMappings.SingleOrDefaultAsync(x => x.Id == mappingId, ct);
        if (mapping is null)
            return false;

        _db.DomainUserRoleMappings.Remove(mapping);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<IdentityUserPermissionMappingResult>> ListUserPermissionMappingsAsync(CancellationToken ct) =>
        await _db.DomainUserPermissionMappings
            .AsNoTracking()
            .Join(
                _db.DomainPermissions.AsNoTracking(),
                mapping => mapping.PermissionId,
                permission => permission.Id,
                (mapping, permission) => ToUserPermissionMappingResult(mapping, permission))
            .OrderBy(x => x.Permission)
            .ThenBy(x => x.Email)
            .ToArrayAsync(ct);

    public async Task<IdentityUserPermissionMappingResult> AssignUserPermissionAsync(
        IdentityUserPermissionAssignment assignment,
        CancellationToken ct)
    {
        var permission = await GetOrCreatePermissionAsync(assignment.Permission, ct);
        var normalizedEmail = NormalizeOptional(assignment.Email);
        var normalizedExternalSubject = NormalizeOptional(assignment.ExternalSubject);
        var userProfileId = await ResolveUserProfileIdAsync(assignment.UserProfileId, normalizedEmail, ct);

        var mapping = await _db.DomainUserPermissionMappings
            .SingleOrDefaultAsync(x =>
                x.PermissionId == permission.Id
                && x.UserProfileId == userProfileId
                && x.ExternalSubject == normalizedExternalSubject
                && x.Email == normalizedEmail,
                ct);

        var now = DateTime.UtcNow;
        if (mapping is null)
        {
            mapping = new DomainUserPermissionMappingEntity
            {
                Id = Guid.NewGuid(),
                PermissionId = permission.Id,
                UserProfileId = userProfileId,
                ExternalSubject = normalizedExternalSubject,
                Email = normalizedEmail,
                IsGranted = assignment.IsGranted,
                IsEnabled = assignment.IsEnabled,
                Reason = NormalizeOptional(assignment.Reason),
                CreatedUtc = now
            };
            _db.DomainUserPermissionMappings.Add(mapping);
        }
        else
        {
            mapping.IsGranted = assignment.IsGranted;
            mapping.IsEnabled = assignment.IsEnabled;
            mapping.Reason = NormalizeOptional(assignment.Reason);
            mapping.UpdatedUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return ToUserPermissionMappingResult(mapping, permission);
    }

    public async Task<bool> DeleteUserPermissionMappingAsync(Guid mappingId, CancellationToken ct)
    {
        var mapping = await _db.DomainUserPermissionMappings.SingleOrDefaultAsync(x => x.Id == mappingId, ct);
        if (mapping is null)
            return false;

        _db.DomainUserPermissionMappings.Remove(mapping);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<IdentityAccessScopeAdminResult>> ListAccessScopesAsync(CancellationToken ct) =>
        await _db.DomainAccessScopes
            .AsNoTracking()
            .OrderBy(x => x.Area)
            .ThenBy(x => x.ScopeType)
            .ThenBy(x => x.ScopeValue)
            .Select(x => ToAccessScopeAdminResult(x))
            .ToArrayAsync(ct);

    public async Task<IdentityAccessScopeAdminResult> UpsertAccessScopeAsync(
        IdentityAccessScopeUpsert upsert,
        CancellationToken ct)
    {
        var entity = upsert.AccessScopeId is not null
            ? await _db.DomainAccessScopes.SingleOrDefaultAsync(x => x.Id == upsert.AccessScopeId.Value, ct)
            : null;

        if (entity is null)
            entity = await GetOrCreateAccessScopeAsync(upsert.Area, upsert.ScopeType, upsert.ScopeValue, upsert.DisplayName, ct);

        entity.Description = NormalizeOptional(upsert.Description);
        entity.DisplayName = NormalizeOptional(upsert.DisplayName) ?? entity.ScopeValue ?? entity.ScopeType;
        entity.IsEnabled = upsert.IsEnabled;
        entity.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToAccessScopeAdminResult(entity);
    }

    public async Task<bool> DeleteAccessScopeAsync(Guid accessScopeId, CancellationToken ct)
    {
        var entity = await _db.DomainAccessScopes.SingleOrDefaultAsync(x => x.Id == accessScopeId, ct);
        if (entity is null)
            return false;

        var mappings = await _db.DomainUserAccessScopeMappings.Where(x => x.AccessScopeId == accessScopeId).ToArrayAsync(ct);
        _db.DomainUserAccessScopeMappings.RemoveRange(mappings);
        _db.DomainAccessScopes.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<IdentityUserAccessScopeMappingResult>> ListUserAccessScopeMappingsAsync(CancellationToken ct) =>
        await _db.DomainUserAccessScopeMappings
            .AsNoTracking()
            .Join(
                _db.DomainAccessScopes.AsNoTracking(),
                mapping => mapping.AccessScopeId,
                scope => scope.Id,
                (mapping, scope) => ToUserAccessScopeMappingResult(mapping, scope))
            .OrderBy(x => x.Area)
            .ThenBy(x => x.ScopeType)
            .ThenBy(x => x.ScopeValue)
            .ThenBy(x => x.Email)
            .ToArrayAsync(ct);

    public async Task<IdentityUserAccessScopeMappingResult> AssignUserAccessScopeAsync(
        IdentityUserAccessScopeAssignment assignment,
        CancellationToken ct)
    {
        var normalizedEmail = NormalizeOptional(assignment.Email);
        var normalizedExternalSubject = NormalizeOptional(assignment.ExternalSubject);
        var userProfileId = await ResolveUserProfileIdAsync(assignment.UserProfileId, normalizedEmail, ct);
        var accessScope = assignment.AccessScopeId is not null
            ? await _db.DomainAccessScopes.SingleOrDefaultAsync(x => x.Id == assignment.AccessScopeId.Value, ct)
            : await GetOrCreateAccessScopeAsync(
                assignment.Area,
                assignment.ScopeType,
                assignment.ScopeValue,
                assignment.DisplayName,
                ct);

        if (accessScope is null)
            throw new InvalidOperationException($"Access scope '{assignment.AccessScopeId}' was not found.");

        var mapping = await _db.DomainUserAccessScopeMappings
            .SingleOrDefaultAsync(x =>
                x.UserProfileId == userProfileId
                && x.ExternalSubject == normalizedExternalSubject
                && x.Email == normalizedEmail
                && x.AccessScopeId == accessScope.Id,
                ct);

        var now = DateTime.UtcNow;
        if (mapping is null)
        {
            mapping = new DomainUserAccessScopeMappingEntity
            {
                Id = Guid.NewGuid(),
                UserProfileId = userProfileId,
                AccessScopeId = accessScope.Id,
                ExternalSubject = normalizedExternalSubject,
                Email = normalizedEmail,
                IsEnabled = assignment.IsEnabled,
                CreatedUtc = now
            };
            _db.DomainUserAccessScopeMappings.Add(mapping);
        }
        else
        {
            mapping.IsEnabled = assignment.IsEnabled;
            mapping.UpdatedUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return ToUserAccessScopeMappingResult(mapping, accessScope);
    }

    public async Task<bool> DeleteUserAccessScopeMappingAsync(Guid mappingId, CancellationToken ct)
    {
        var mapping = await _db.DomainUserAccessScopeMappings.SingleOrDefaultAsync(x => x.Id == mappingId, ct);
        if (mapping is null)
            return false;

        _db.DomainUserAccessScopeMappings.Remove(mapping);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Guid?> ResolveUserProfileIdAsync(Guid? userProfileId, string? email, CancellationToken ct)
    {
        if (userProfileId is not null)
        {
            var exists = await _db.DomainUserProfiles.AnyAsync(x => x.Id == userProfileId.Value, ct);
            if (!exists)
                throw new InvalidOperationException($"User profile '{userProfileId}' was not found.");

            return userProfileId.Value;
        }

        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await _db.DomainUserProfiles
            .AsNoTracking()
            .Where(x => x.Email == email)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(ct);
    }

    private async Task<DomainRoleEntity> GetOrCreateRoleAsync(string role, CancellationToken ct)
    {
        var normalized = NormalizeRequired(role);
        var entity = await _db.DomainRoles.SingleOrDefaultAsync(x => x.Role == normalized, ct);
        if (entity is not null)
            return entity;

        entity = new DomainRoleEntity
        {
            Id = Guid.NewGuid(),
            Role = normalized,
            CreatedUtc = DateTime.UtcNow
        };
        _db.DomainRoles.Add(entity);
        return entity;
    }

    private async Task<DomainPermissionEntity> GetOrCreatePermissionAsync(string permission, CancellationToken ct)
    {
        var normalized = NormalizeRequired(permission);
        var entity = await _db.DomainPermissions.SingleOrDefaultAsync(x => x.Permission == normalized, ct);
        if (entity is not null)
            return entity;

        entity = new DomainPermissionEntity
        {
            Id = Guid.NewGuid(),
            Permission = normalized,
            DisplayName = ToDisplayName(normalized),
            Category = ToCategory(normalized),
            IsEnabled = true,
            CreatedUtc = DateTime.UtcNow
        };
        _db.DomainPermissions.Add(entity);
        return entity;
    }

    private async Task<DomainAccessScopeEntity> GetOrCreateAccessScopeAsync(
        string area,
        string scopeType,
        string? scopeValue,
        string? displayName,
        CancellationToken ct)
    {
        var normalizedArea = NormalizeRequired(area);
        var normalizedScopeType = NormalizeRequired(scopeType);
        var normalizedScopeValue = NormalizeOptional(scopeValue);
        var normalizedDisplayName = NormalizeOptional(displayName) ?? normalizedScopeValue ?? normalizedScopeType;

        var entity = await _db.DomainAccessScopes.SingleOrDefaultAsync(x =>
            x.Area == normalizedArea
            && x.ScopeType == normalizedScopeType
            && x.ScopeValue == normalizedScopeValue,
            ct);

        if (entity is not null)
        {
            entity.DisplayName = normalizedDisplayName;
            entity.IsEnabled = true;
            entity.UpdatedUtc = DateTime.UtcNow;
            return entity;
        }

        entity = new DomainAccessScopeEntity
        {
            Id = Guid.NewGuid(),
            Area = normalizedArea,
            ScopeType = normalizedScopeType,
            ScopeValue = normalizedScopeValue,
            DisplayName = normalizedDisplayName,
            IsEnabled = true,
            CreatedUtc = DateTime.UtcNow
        };
        _db.DomainAccessScopes.Add(entity);
        return entity;
    }

    private async Task UpsertRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissions, CancellationToken ct)
    {
        var normalizedPermissions = permissions
            .Select(NormalizeOptional)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var permission in normalizedPermissions)
        {
            var permissionEntity = await GetOrCreatePermissionAsync(permission, ct);
            if (await _db.DomainRolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionEntity.Id, ct))
                continue;

            _db.DomainRolePermissions.Add(new DomainRolePermissionEntity
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                PermissionId = permissionEntity.Id,
                CreatedUtc = DateTime.UtcNow
            });
        }
    }

    private async Task<IdentityRoleAdminResult> GetRoleResultAsync(Guid roleId, CancellationToken ct)
    {
        var role = await _db.DomainRoles.AsNoTracking().SingleAsync(x => x.Id == roleId, ct);
        var permissions = await _db.DomainRolePermissions
            .AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .Join(
                _db.DomainPermissions.AsNoTracking().Where(x => x.IsEnabled),
                rolePermission => rolePermission.PermissionId,
                permission => permission.Id,
                (_, permission) => permission.Permission)
            .OrderBy(x => x)
            .ToArrayAsync(ct);

        return new IdentityRoleAdminResult
        {
            RoleId = role.Id,
            Role = role.Role,
            Permissions = permissions
        };
    }

    private static string NormalizeRequired(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", nameof(value))
            : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ToCategory(string permission)
    {
        var separator = permission.IndexOf('.', StringComparison.Ordinal);
        return separator > 0 ? permission[..separator] : "General";
    }

    private static string ToDisplayName(string permission) =>
        permission.Replace(".", " ", StringComparison.Ordinal);

    private static IdentityUserProfileResult ToUserProfileResult(DomainUserProfileEntity profile) =>
        new()
        {
            UserProfileId = profile.Id,
            ExternalSubject = profile.ExternalSubject,
            Email = profile.Email,
            DisplayName = profile.DisplayName,
            AdviserId = profile.AdviserId,
            JobRole = profile.JobRole,
            Status = profile.Status
        };

    private static IdentityPermissionResult ToPermissionResult(DomainPermissionEntity permission) =>
        new()
        {
            PermissionId = permission.Id,
            Permission = permission.Permission,
            DisplayName = permission.DisplayName,
            Description = permission.Description,
            Category = permission.Category,
            IsEnabled = permission.IsEnabled
        };

    private static IdentityUserRoleMappingResult ToUserRoleMappingResult(
        DomainUserRoleMappingEntity mapping,
        DomainRoleEntity role) =>
        new()
        {
            MappingId = mapping.Id,
            UserProfileId = mapping.UserProfileId,
            RoleId = role.Id,
            Role = role.Role,
            Email = mapping.Email,
            ExternalRole = mapping.ExternalRole,
            ExternalGroupId = mapping.ExternalGroupId,
            IsEnabled = mapping.IsEnabled
        };

    private static IdentityUserPermissionMappingResult ToUserPermissionMappingResult(
        DomainUserPermissionMappingEntity mapping,
        DomainPermissionEntity permission) =>
        new()
        {
            MappingId = mapping.Id,
            UserProfileId = mapping.UserProfileId,
            PermissionId = permission.Id,
            Permission = permission.Permission,
            ExternalSubject = mapping.ExternalSubject,
            Email = mapping.Email,
            IsGranted = mapping.IsGranted,
            IsEnabled = mapping.IsEnabled,
            Reason = mapping.Reason
        };

    private static IdentityAccessScopeAdminResult ToAccessScopeAdminResult(DomainAccessScopeEntity scope) =>
        new()
        {
            AccessScopeId = scope.Id,
            Area = scope.Area,
            ScopeType = scope.ScopeType,
            ScopeValue = scope.ScopeValue,
            DisplayName = scope.DisplayName,
            Description = scope.Description,
            IsEnabled = scope.IsEnabled
        };

    private static IdentityUserAccessScopeMappingResult ToUserAccessScopeMappingResult(
        DomainUserAccessScopeMappingEntity mapping,
        DomainAccessScopeEntity scope) =>
        new()
        {
            MappingId = mapping.Id,
            UserProfileId = mapping.UserProfileId,
            AccessScopeId = scope.Id,
            ExternalSubject = mapping.ExternalSubject,
            Email = mapping.Email,
            Area = scope.Area,
            ScopeType = scope.ScopeType,
            ScopeValue = scope.ScopeValue,
            DisplayName = scope.DisplayName,
            IsEnabled = mapping.IsEnabled
        };
}
