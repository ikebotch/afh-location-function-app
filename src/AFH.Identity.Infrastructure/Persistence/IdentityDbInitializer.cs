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

        var fullControlCentrePermissions = new[]
        {
            DashboardRead,
            AdviserRead,
            AdviserManage,
            AdviserSpecialismsManage,
            CalendarRead,
            CalendarManage,
            CalendarSlotsOverride,
            CoverageRead,
            CoverageManage,
            NotificationsRead,
            NotificationsTemplatesRead,
            NotificationsTemplatesManage,
            NotificationsSettingsRead,
            NotificationsSettingsManage,
            ReportingRead,
            AuditRead,
            SystemRead,
            SystemManage,
            SystemHealthRead,
            SystemAuditRead,
            SystemDiagnosticsRead,
            UsersRead,
            RbacRead,
            RbacManage,
            PartnersRead
        };

        var operationsControlCentrePermissions = fullControlCentrePermissions
            .Where(permission => permission != RbacManage && permission != SystemManage)
            .ToArray();

        var operationsPermissions = new[]
        {
            OrganisationAssignmentPermissions.Read,
            OrganisationAssignmentPermissions.Create,
            OrganisationAssignmentPermissions.Update,
            OrganisationAssignmentPermissions.Disable,
            OrganisationAssignmentPermissions.Delete,
            OrganisationAssignmentsManage,
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
        }.Concat(operationsControlCentrePermissions).ToArray();

        var adminPermissions = new[]
        {
            OrganisationAssignmentPermissions.Read,
            OrganisationAssignmentPermissions.Create,
            OrganisationAssignmentPermissions.Update,
            OrganisationAssignmentPermissions.Disable,
            OrganisationAssignmentPermissions.Delete,
            OrganisationAssignmentsManage,
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
        }.Concat(fullControlCentrePermissions).ToArray();

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
                BookingPermissionNames.ApprovalsReview,
                DashboardRead,
                BookingPermissionNames.AdminRead
            ],
            ["LeadTech"] =
            [
                OrganisationAssignmentPermissions.Read,
                DashboardRead,
                BookingPermissionNames.AdminRead,
                BookingPermissionNames.ApprovalsRead,
                BookingPermissionNames.CancelAsLeadTech,
                BookingPermissionNames.RearrangeAsLeadTech,
                BookingPermissionNames.RearrangementOptionsRead,
                AdviserRead,
                CalendarRead,
                CoverageRead
            ],
            ["Manager"] =
            [
                OrganisationAssignmentPermissions.Read,
                DashboardRead,
                BookingPermissionNames.AdminRead,
                BookingPermissionNames.ApprovalsRead,
                BookingPermissionNames.ApprovalsReview,
                BookingPermissionNames.CancelDirect,
                BookingPermissionNames.RearrangeDirect,
                AdviserRead,
                CalendarRead,
                CoverageRead,
                ReportingRead
            ],
            ["Operations"] = operationsPermissions,
            ["Admin"] = adminPermissions
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

    private const string DashboardRead = "Dashboard.Read";
    private const string RbacRead = "Rbac.Read";
    private const string RbacManage = "Rbac.Manage";
    private const string OrganisationAssignmentsManage = "OrganisationAssignments.Manage";
    private const string AdviserRead = "Advisers.Read";
    private const string AdviserManage = "Advisers.Manage";
    private const string AdviserSpecialismsManage = "Advisers.Specialisms.Manage";
    private const string CalendarRead = "Calendar.Read";
    private const string CalendarManage = "Calendar.Manage";
    private const string CalendarSlotsOverride = "Calendar.Slots.Override";
    private const string CoverageRead = "Coverage.Read";
    private const string CoverageManage = "Coverage.Manage";
    private const string NotificationsRead = "Notifications.Read";
    private const string NotificationsTemplatesRead = "Notifications.Templates.Read";
    private const string NotificationsTemplatesManage = "Notifications.Templates.Manage";
    private const string NotificationsSettingsRead = "Notifications.Settings.Read";
    private const string NotificationsSettingsManage = "Notifications.Settings.Manage";
    private const string ReportingRead = "Reporting.Read";
    private const string AuditRead = "Audit.Read";
    private const string SystemRead = "System.Read";
    private const string SystemManage = "System.Manage";
    private const string SystemHealthRead = "System.Health.Read";
    private const string SystemAuditRead = "System.Audit.Read";
    private const string SystemDiagnosticsRead = "System.Diagnostics.Read";
    private const string UsersRead = "Users.Read";
    private const string PartnersRead = "Partners.Read";
}
