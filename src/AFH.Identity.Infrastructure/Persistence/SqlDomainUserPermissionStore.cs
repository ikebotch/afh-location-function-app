using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Adviser.Application.Models.Auth;
using AFH.Identity.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFH.Identity.Infrastructure.Persistence;

public sealed class SqlDomainUserPermissionStore : IDomainUserPermissionStore
{
    private readonly IdentityDbContext _db;
    private readonly IdentityRbacOptions _options;

    public SqlDomainUserPermissionStore(
        IdentityDbContext db,
        IOptions<IdentityRbacOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<bool> HasPermissionAsync(
        DomainUserIdentity identity,
        string permission,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identity.Email) || string.IsNullOrWhiteSpace(permission))
            return false;

        var email = identity.Email.Trim();
        var externalSubject = identity.UserId.Trim();
        var appRoles = identity.AppRoles.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        var groups = identity.Groups.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

        var profile = await _db.DomainUserProfiles
            .AsNoTracking()
            .Where(x => x.ExternalSubject == externalSubject || x.Email == email)
            .OrderByDescending(x => x.ExternalSubject == externalSubject)
            .FirstOrDefaultAsync(ct);

        var directGranted = false;
        if (profile is not null && _options.PermissionMode is not IdentityPermissionResolutionMode.RolesOnly)
        {
            var userPermissions = await _db.DomainUserPermissionMappings
                .AsNoTracking()
                .Where(x => x.IsEnabled)
                .Where(x =>
                    (x.UserProfileId != null && x.UserProfileId == profile.Id)
                    || (x.ExternalSubject != null && x.ExternalSubject == profile.ExternalSubject)
                    || (x.Email != null && x.Email == email))
                .Join(
                    _db.DomainPermissions.AsNoTracking().Where(x => x.IsEnabled && x.Permission == permission),
                    mapping => mapping.PermissionId,
                    permissionEntity => permissionEntity.Id,
                    (mapping, _) => mapping.IsGranted)
                .ToArrayAsync(ct);

            if (userPermissions.Any(x => !x))
                return false;

            directGranted = userPermissions.Any(x => x);
            if (directGranted && _options.PermissionMode is IdentityPermissionResolutionMode.UserOnly)
                return true;
        }

        if (_options.PermissionMode is IdentityPermissionResolutionMode.UserOnly)
            return false;

        var roleIds = await _db.DomainUserRoleMappings
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .Where(x =>
                (profile != null && x.UserProfileId != null && x.UserProfileId == profile.Id)
                || (x.Email != null && x.Email == email)
                || (x.ExternalRole != null && appRoles.Contains(x.ExternalRole))
                || (x.ExternalGroupId != null && groups.Contains(x.ExternalGroupId)))
            .Select(x => x.RoleId)
            .Distinct()
            .ToArrayAsync(ct);

        if (roleIds.Length == 0)
            return directGranted;

        return directGranted || await _db.DomainRolePermissions
            .AsNoTracking()
            .Where(x => roleIds.Contains(x.RoleId))
            .Join(
                _db.DomainPermissions.AsNoTracking().Where(x => x.IsEnabled && x.Permission == permission),
                rolePermission => rolePermission.PermissionId,
                permissionEntity => permissionEntity.Id,
                (_, _) => true)
            .AnyAsync(ct);
    }
}
