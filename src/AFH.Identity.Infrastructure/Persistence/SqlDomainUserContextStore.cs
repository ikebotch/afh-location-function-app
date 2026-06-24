using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Adviser.Application.Models.Auth;
using AFH.Identity.Infrastructure.Persistence.Entities;
using AFH.Identity.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFH.Identity.Infrastructure.Persistence;

public sealed class SqlDomainUserContextStore : IDomainUserContextStore
{
    private readonly IdentityDbContext _db;
    private readonly IdentityRbacOptions _options;

    public SqlDomainUserContextStore(
        IdentityDbContext db,
        IOptions<IdentityRbacOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<DomainUserContext> GetContextAsync(DomainUserIdentity identity, CancellationToken ct)
    {
        var profile = await GetOrCreateProfileAsync(identity, ct);
        var email = identity.Email.Trim();
        var appRoles = identity.AppRoles.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        var groups = identity.Groups.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

        var matchedRoleIds = await _db.DomainUserRoleMappings
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .Where(x =>
                (x.UserProfileId != null && x.UserProfileId == profile.Id)
                || (x.Email != null && x.Email == email)
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

        var permissions = await ResolvePermissionsAsync(profile.Id, profile.ExternalSubject, email, matchedRoleIds, ct);

        return new DomainUserContext(
            profile.Id.ToString("D"),
            profile.ExternalSubject,
            profile.Email,
            profile.DisplayName,
            profile.AdviserId,
            profile.JobRole,
            roles,
            permissions);
    }

    private async Task<IReadOnlyList<string>> ResolvePermissionsAsync(
        Guid userProfileId,
        string externalSubject,
        string email,
        IReadOnlyList<Guid> roleIds,
        CancellationToken ct)
    {
        var rolePermissions = _options.PermissionMode is IdentityPermissionResolutionMode.UserOnly || roleIds.Count == 0
            ? []
            : await _db.DomainRolePermissions
                .AsNoTracking()
                .Where(x => roleIds.Contains(x.RoleId))
                .Join(
                    _db.DomainPermissions.AsNoTracking().Where(x => x.IsEnabled),
                    rolePermission => rolePermission.PermissionId,
                    permission => permission.Id,
                    (_, permission) => permission.Permission)
                .Distinct()
                .ToArrayAsync(ct);

        if (_options.PermissionMode is IdentityPermissionResolutionMode.RolesOnly)
        {
            return rolePermissions
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToArray();
        }

        var userMappings = await _db.DomainUserPermissionMappings
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .Where(x =>
                (x.UserProfileId != null && x.UserProfileId == userProfileId)
                || (x.ExternalSubject != null && x.ExternalSubject == externalSubject)
                || (x.Email != null && x.Email == email))
            .Join(
                _db.DomainPermissions.AsNoTracking().Where(x => x.IsEnabled),
                mapping => mapping.PermissionId,
                permission => permission.Id,
                (mapping, permission) => new { permission.Permission, mapping.IsGranted })
            .ToArrayAsync(ct);

        var denied = userMappings
            .Where(x => !x.IsGranted)
            .Select(x => x.Permission)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rolePermissions
            .Concat(userMappings.Where(x => x.IsGranted).Select(x => x.Permission))
            .Where(x => !denied.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();
    }

    private async Task<DomainUserProfileEntity> GetOrCreateProfileAsync(DomainUserIdentity identity, CancellationToken ct)
    {
        var externalSubject = identity.UserId.Trim();
        var email = identity.Email.Trim();
        var displayName = string.IsNullOrWhiteSpace(identity.DisplayName) ? email : identity.DisplayName.Trim();

        var profile = await _db.DomainUserProfiles.SingleOrDefaultAsync(x => x.ExternalSubject == externalSubject, ct);
        var now = DateTime.UtcNow;
        if (profile is null)
        {
            profile = await _db.DomainUserProfiles.SingleOrDefaultAsync(x => x.Email == email, ct);
            if (profile is null)
            {
                profile = new DomainUserProfileEntity
                {
                    Id = Guid.NewGuid(),
                    ExternalSubject = externalSubject,
                    Email = email,
                    DisplayName = displayName,
                    JobRole = null,
                    Status = "Active",
                    CreatedUtc = now
                };
                _db.DomainUserProfiles.Add(profile);
            }
            else
            {
                profile.ExternalSubject = externalSubject;
                profile.UpdatedUtc = now;
            }
        }

        if (!string.Equals(profile.Email, email, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(profile.DisplayName, displayName, StringComparison.Ordinal))
        {
            profile.Email = email;
            profile.DisplayName = displayName;
            profile.UpdatedUtc = now;
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(ct);

        return profile;
    }
}
