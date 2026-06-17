using AFH.Adviser.Infrastructure.Persistence.Auth.Entities;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;
using AFH.Identity.Application.Abstractions;
using AFH.Identity.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Identity.Infrastructure.Services;

public sealed class IdentityRbacAdminService : IIdentityRbacAdminService
{
    private readonly AdviserDirectoryDbContext _db;

    public IdentityRbacAdminService(AdviserDirectoryDbContext db)
    {
        _db = db;
    }

    public async Task<IdentityRoleAdminResult> UpsertRoleAsync(
        string role,
        IReadOnlyList<string> permissions,
        CancellationToken ct)
    {
        var entity = await GetOrCreateRoleAsync(role, ct);
        await UpsertPermissionsAsync(entity.Id, permissions, ct);
        await _db.SaveChangesAsync(ct);
        return await GetRoleResultAsync(entity.Id, ct);
    }

    public async Task<IdentityRoleAdminResult> AddRolePermissionAsync(
        string role,
        string permission,
        CancellationToken ct)
    {
        var entity = await GetOrCreateRoleAsync(role, ct);
        await UpsertPermissionsAsync(entity.Id, [permission], ct);
        await _db.SaveChangesAsync(ct);
        return await GetRoleResultAsync(entity.Id, ct);
    }

    public async Task<IdentityUserRoleMappingResult> AssignUserRoleAsync(
        IdentityUserRoleAssignment assignment,
        CancellationToken ct)
    {
        var role = await GetOrCreateRoleAsync(assignment.Role, ct);
        var normalizedEmail = NormalizeOptional(assignment.Email);
        var normalizedExternalRole = NormalizeOptional(assignment.ExternalRole);
        var normalizedExternalGroupId = NormalizeOptional(assignment.ExternalGroupId);

        var mapping = await _db.DomainUserRoleMappings
            .SingleOrDefaultAsync(x =>
                x.RoleId == role.Id
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

        return new IdentityUserRoleMappingResult
        {
            MappingId = mapping.Id,
            RoleId = role.Id,
            Role = role.Role,
            Email = mapping.Email,
            ExternalRole = mapping.ExternalRole,
            ExternalGroupId = mapping.ExternalGroupId,
            IsEnabled = mapping.IsEnabled
        };
    }

    public async Task<IdentityUserPermissionMappingResult> AssignUserPermissionAsync(
        IdentityUserPermissionAssignment assignment,
        CancellationToken ct)
    {
        var normalizedPermission = NormalizeRequired(assignment.Permission);
        var permission = await GetOrCreatePermissionAsync(normalizedPermission, ct);
        var normalizedEmail = NormalizeOptional(assignment.Email);
        var normalizedExternalRole = NormalizeOptional(assignment.ExternalRole);
        var normalizedExternalGroupId = NormalizeOptional(assignment.ExternalGroupId);

        var mapping = await _db.DomainUserPermissionMappings
            .SingleOrDefaultAsync(x =>
                x.PermissionId == permission.Id
                && x.Email == normalizedEmail
                && x.ExternalRole == normalizedExternalRole
                && x.ExternalGroupId == normalizedExternalGroupId,
                ct);

        var now = DateTime.UtcNow;
        if (mapping is null)
        {
            mapping = new DomainUserPermissionMappingEntity
            {
                Id = Guid.NewGuid(),
                PermissionId = permission.Id,
                Email = normalizedEmail,
                ExternalRole = normalizedExternalRole,
                ExternalGroupId = normalizedExternalGroupId,
                IsEnabled = assignment.IsEnabled,
                CreatedUtc = now
            };
            _db.DomainUserPermissionMappings.Add(mapping);
        }
        else
        {
            mapping.IsEnabled = assignment.IsEnabled;
            mapping.UpdatedUtc = now;
        }

        await _db.SaveChangesAsync(ct);

        return new IdentityUserPermissionMappingResult
        {
            MappingId = mapping.Id,
            Permission = permission.Permission,
            Email = mapping.Email,
            ExternalRole = mapping.ExternalRole,
            ExternalGroupId = mapping.ExternalGroupId,
            IsEnabled = mapping.IsEnabled
        };
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

    private async Task UpsertPermissionsAsync(Guid roleId, IReadOnlyList<string> permissions, CancellationToken ct)
    {
        var normalizedPermissions = permissions
            .Select(NormalizeOptional)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPermissions.Length == 0)
            return;

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
}
