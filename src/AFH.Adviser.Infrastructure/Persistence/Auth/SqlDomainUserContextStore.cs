using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Adviser.Application.Models.Auth;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Infrastructure.Persistence.Auth;

public sealed class SqlDomainUserContextStore : IDomainUserContextStore
{
    private readonly AdviserDirectoryDbContext _db;

    public SqlDomainUserContextStore(AdviserDirectoryDbContext db)
    {
        _db = db;
    }

    public async Task<DomainUserContext> GetContextAsync(DomainUserIdentity identity, CancellationToken ct)
    {
        var email = identity.Email.Trim();
        var appRoles = identity.AppRoles.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        var groups = identity.Groups.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

        var matchedRoleIds = await _db.DomainUserRoleMappings
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .Where(x =>
                (x.Email != null && x.Email == email)
                || (x.ExternalRole != null && appRoles.Contains(x.ExternalRole))
                || (x.ExternalGroupId != null && groups.Contains(x.ExternalGroupId)))
            .Select(x => x.RoleId)
            .Distinct()
            .ToArrayAsync(ct);

        var roles = matchedRoleIds.Length == 0
            ? []
            : await _db.DomainRoles
            .AsNoTracking()
            .Where(x => matchedRoleIds.Contains(x.Id))
            .Select(x => x.Role)
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(ct);

        var rolePermissions = matchedRoleIds.Length == 0
            ? []
            : await _db.DomainRolePermissions
            .AsNoTracking()
            .Where(x => matchedRoleIds.Contains(x.RoleId))
            .Join(
                _db.DomainPermissions.AsNoTracking().Where(x => x.IsEnabled),
                rolePermission => rolePermission.PermissionId,
                permission => permission.Id,
                (_, permission) => permission.Permission)
            .Distinct()
            .ToArrayAsync(ct);

        var userPermissions = await _db.DomainUserPermissionMappings
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .Where(x =>
                (x.Email != null && x.Email == email)
                || (x.ExternalRole != null && appRoles.Contains(x.ExternalRole))
                || (x.ExternalGroupId != null && groups.Contains(x.ExternalGroupId)))
            .Join(
                _db.DomainPermissions.AsNoTracking().Where(x => x.IsEnabled),
                userPermission => userPermission.PermissionId,
                permission => permission.Id,
                (_, permission) => permission.Permission)
            .Distinct()
            .ToArrayAsync(ct);

        var permissions = rolePermissions
            .Concat(userPermissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        return new DomainUserContext(identity.UserId, identity.Email, identity.DisplayName, roles, permissions);
    }
}
