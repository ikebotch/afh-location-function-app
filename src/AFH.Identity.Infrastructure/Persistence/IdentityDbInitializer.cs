using AFH.Adviser.Application.Models.Auth;
using AFH.Identity.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AFH.Identity.Infrastructure.Persistence;

public sealed class IdentityDbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public IdentityDbInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await SeedDomainRbacAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedDomainRbacAsync(IdentityDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var roleNames = new[] { "Adviser", "Approver", "LeadTech", "Manager", "Operations", "Admin" };
        foreach (var roleName in roleNames)
        {
            if (!await db.DomainRoles.AnyAsync(x => x.Role == roleName, ct))
            {
                db.DomainRoles.Add(new DomainRoleEntity
                {
                    Id = Guid.NewGuid(),
                    Role = roleName,
                    CreatedUtc = now
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var permissionsByRole = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Adviser"] =
            [
                BookingPermissionNames.ApprovalRequestsCreate,
                BookingPermissionNames.ApprovalRequestsReadOwn
            ],
            ["Approver"] =
            [
                OrganisationAssignmentPermissions.Read,
                BookingPermissionNames.ApprovalsRead,
                BookingPermissionNames.ApprovalsReview
            ],
            ["LeadTech"] =
            [
                OrganisationAssignmentPermissions.Read,
                BookingPermissionNames.ApprovalsRead,
                BookingPermissionNames.CancelAsLeadTech,
                BookingPermissionNames.RearrangeAsLeadTech,
                BookingPermissionNames.RearrangementOptionsRead
            ],
            ["Manager"] =
            [
                OrganisationAssignmentPermissions.Read,
                BookingPermissionNames.ApprovalsRead,
                BookingPermissionNames.ApprovalsReview,
                BookingPermissionNames.CancelDirect,
                BookingPermissionNames.RearrangeDirect
            ],
            ["Operations"] =
            [
                OrganisationAssignmentPermissions.Read,
                OrganisationAssignmentPermissions.Create,
                OrganisationAssignmentPermissions.Update,
                OrganisationAssignmentPermissions.Disable,
                OrganisationAssignmentPermissions.Delete,
                BookingPermissionNames.ApprovalsRead,
                BookingPermissionNames.ApprovalsReview,
                BookingPermissionNames.ApprovalRequestsCreate,
                BookingPermissionNames.ApprovalRequestsReadOwn,
                BookingPermissionNames.CancelAsLeadTech,
                BookingPermissionNames.CancelDirect,
                BookingPermissionNames.RearrangeAsLeadTech,
                BookingPermissionNames.RearrangeDirect,
                BookingPermissionNames.RearrangementOptionsRead,
                BookingPermissionNames.AdminRead
            ],
            ["Admin"] =
            [
                OrganisationAssignmentPermissions.Read,
                OrganisationAssignmentPermissions.Create,
                OrganisationAssignmentPermissions.Update,
                OrganisationAssignmentPermissions.Disable,
                OrganisationAssignmentPermissions.Delete,
                BookingPermissionNames.ApprovalsRead,
                BookingPermissionNames.ApprovalsReview,
                BookingPermissionNames.ApprovalRequestsCreate,
                BookingPermissionNames.ApprovalRequestsReadOwn,
                BookingPermissionNames.CancelAsLeadTech,
                BookingPermissionNames.CancelDirect,
                BookingPermissionNames.RearrangeAsLeadTech,
                BookingPermissionNames.RearrangeDirect,
                BookingPermissionNames.RearrangementOptionsRead,
                BookingPermissionNames.AdminRead
            ]
        };

        var permissionNames = permissionsByRole.Values
            .SelectMany(x => x)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var permissionName in permissionNames)
        {
            if (!await db.DomainPermissions.AnyAsync(x => x.Permission == permissionName, ct))
            {
                db.DomainPermissions.Add(new DomainPermissionEntity
                {
                    Id = Guid.NewGuid(),
                    Permission = permissionName,
                    DisplayName = ToDisplayName(permissionName),
                    Category = ToCategory(permissionName),
                    IsEnabled = true,
                    CreatedUtc = now
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var roles = await db.DomainRoles.ToDictionaryAsync(x => x.Role, x => x.Id, StringComparer.OrdinalIgnoreCase, ct);
        var permissionIds = await db.DomainPermissions.ToDictionaryAsync(x => x.Permission, x => x.Id, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var (role, permissions) in permissionsByRole)
        {
            if (!roles.TryGetValue(role, out var roleId))
                continue;

            foreach (var permission in permissions)
            {
                if (!permissionIds.TryGetValue(permission, out var permissionId))
                    continue;

                if (!await db.DomainRolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct))
                {
                    db.DomainRolePermissions.Add(new DomainRolePermissionEntity
                    {
                        Id = Guid.NewGuid(),
                        RoleId = roleId,
                        PermissionId = permissionId,
                        CreatedUtc = now
                    });
                }
            }
        }

        foreach (var (role, roleId) in roles)
        {
            if (!await db.DomainUserRoleMappings.AnyAsync(x => x.RoleId == roleId && x.ExternalRole == role, ct))
            {
                db.DomainUserRoleMappings.Add(new DomainUserRoleMappingEntity
                {
                    Id = Guid.NewGuid(),
                    RoleId = roleId,
                    ExternalRole = role,
                    IsEnabled = true,
                    CreatedUtc = now
                });
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
    }

    private static string ToCategory(string permission)
    {
        var separator = permission.IndexOf('.', StringComparison.Ordinal);
        return separator > 0 ? permission[..separator] : "General";
    }

    private static string ToDisplayName(string permission) =>
        permission.Replace(".", " ", StringComparison.Ordinal);
}
