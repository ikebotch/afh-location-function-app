using AFH.Location.Application.Abstractions.Auth;
using AFH.Location.Application.Models.Auth;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class SqlDomainUserPermissionStore : IDomainUserPermissionStore
{
    private readonly LocationPolicyDbContext _db;

    public SqlDomainUserPermissionStore(LocationPolicyDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermissionAsync(
        DomainUserIdentity identity,
        string permission,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identity.Email) || string.IsNullOrWhiteSpace(permission))
            return false;

        var email = identity.Email.Trim();
        var appRoles = identity.AppRoles.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        var groups = identity.Groups.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

        var roleIds = await _db.DomainUserRoleMappings
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .Where(x =>
                (x.Email != null && x.Email == email)
                || (x.ExternalRole != null && appRoles.Contains(x.ExternalRole))
                || (x.ExternalGroupId != null && groups.Contains(x.ExternalGroupId)))
            .Select(x => x.RoleId)
            .Distinct()
            .ToArrayAsync(ct);

        if (roleIds.Length == 0)
            return false;

        return await _db.DomainRolePermissions
            .AsNoTracking()
            .AnyAsync(x => roleIds.Contains(x.RoleId) && x.Permission == permission, ct);
    }
}
