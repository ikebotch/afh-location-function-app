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
        var accessScopes = await ResolveAccessScopesAsync(profile, email, roles, permissions, ct);

        return new DomainUserContext(
            profile.Id.ToString("D"),
            profile.ExternalSubject,
            profile.Email,
            profile.DisplayName,
            profile.AdviserId,
            profile.JobRole,
            roles,
            permissions,
            accessScopes);
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

    private async Task<IReadOnlyList<DomainAccessScope>> ResolveAccessScopesAsync(
        DomainUserProfileEntity profile,
        string email,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        CancellationToken ct)
    {
        var scopes = new List<DomainAccessScope>();

        if (permissions.Contains("*", StringComparer.OrdinalIgnoreCase)
            || roles.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            || roles.Contains("Operations", StringComparer.OrdinalIgnoreCase))
        {
            scopes.Add(new DomainAccessScope("*", "All", null, "All records"));
        }
        else if (roles.Contains("Manager", StringComparer.OrdinalIgnoreCase))
        {
            scopes.Add(new DomainAccessScope("*", "Organisation", null, "Organisation"));
        }

        if (!string.IsNullOrWhiteSpace(profile.AdviserId))
        {
            var adviserId = profile.AdviserId.Trim();
            scopes.Add(new DomainAccessScope("Bookings", "AdviserSelf", adviserId, "Own adviser bookings"));
            scopes.Add(new DomainAccessScope("Advisers", "AdviserSelf", adviserId, "Own adviser profile"));
            scopes.Add(new DomainAccessScope("Calendar", "AdviserSelf", adviserId, "Own adviser calendar"));
        }

        var explicitScopes = await _db.DomainUserAccessScopeMappings
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .Where(x =>
                (x.UserProfileId != null && x.UserProfileId == profile.Id)
                || (x.ExternalSubject != null && x.ExternalSubject == profile.ExternalSubject)
                || (x.Email != null && x.Email == email))
            .Join(
                _db.DomainAccessScopes.AsNoTracking().Where(x => x.IsEnabled),
                mapping => mapping.AccessScopeId,
                scope => scope.Id,
                (_, scope) => new DomainAccessScope(scope.Area, scope.ScopeType, scope.ScopeValue, scope.DisplayName))
            .ToArrayAsync(ct);

        scopes.AddRange(explicitScopes);

        return scopes
            .Where(x => !string.IsNullOrWhiteSpace(x.Area) && !string.IsNullOrWhiteSpace(x.ScopeType))
            .GroupBy(x => $"{x.Area.Trim()}|{x.ScopeType.Trim()}|{x.ScopeValue?.Trim() ?? string.Empty}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Area)
            .ThenBy(x => x.ScopeType)
            .ThenBy(x => x.ScopeValue)
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
