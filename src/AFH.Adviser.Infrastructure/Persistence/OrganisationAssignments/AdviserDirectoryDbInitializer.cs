using AFH.Adviser.Application.Models.Auth;
using AFH.Adviser.Infrastructure.Persistence.Auth.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;

public sealed class AdviserDirectoryDbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public AdviserDirectoryDbInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdviserDirectoryDbContext>();

        await db.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureOrganisationAssignmentsTableAsync(db, cancellationToken);
        await EnsureDomainRbacTablesAsync(db, cancellationToken);
        await SeedDomainRbacAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static Task EnsureOrganisationAssignmentsTableAsync(AdviserDirectoryDbContext db, CancellationToken ct)
        => db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[OrganisationAssignments]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[OrganisationAssignments] (
                    [Id] uniqueidentifier NOT NULL,
                    [Context] nvarchar(100) NOT NULL,
                    [AssignmentType] nvarchar(100) NOT NULL,
                    [OrganisationId] nvarchar(100) NULL,
                    [ClientId] nvarchar(100) NULL,
                    [Region] nvarchar(128) NULL,
                    [AdviserId] nvarchar(100) NULL,
                    [DisplayName] nvarchar(200) NOT NULL,
                    [Email] nvarchar(320) NULL,
                    [MobileNumber] nvarchar(50) NULL,
                    [Channels] nvarchar(200) NOT NULL,
                    [IsEnabled] bit NOT NULL,
                    [Priority] int NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    [UpdatedUtc] datetime2 NULL,
                    CONSTRAINT [PK_OrganisationAssignments] PRIMARY KEY ([Id])
                );
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OrganisationAssignments_Context_AssignmentType_IsEnabled_Priority' AND [object_id] = OBJECT_ID(N'[dbo].[OrganisationAssignments]'))
                CREATE INDEX [IX_OrganisationAssignments_Context_AssignmentType_IsEnabled_Priority] ON [dbo].[OrganisationAssignments] ([Context], [AssignmentType], [IsEnabled], [Priority]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OrganisationAssignments_Region' AND [object_id] = OBJECT_ID(N'[dbo].[OrganisationAssignments]'))
                CREATE INDEX [IX_OrganisationAssignments_Region] ON [dbo].[OrganisationAssignments] ([Region]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OrganisationAssignments_AdviserId' AND [object_id] = OBJECT_ID(N'[dbo].[OrganisationAssignments]'))
                CREATE INDEX [IX_OrganisationAssignments_AdviserId] ON [dbo].[OrganisationAssignments] ([AdviserId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OrganisationAssignments_ClientId' AND [object_id] = OBJECT_ID(N'[dbo].[OrganisationAssignments]'))
                CREATE INDEX [IX_OrganisationAssignments_ClientId] ON [dbo].[OrganisationAssignments] ([ClientId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OrganisationAssignments_OrganisationId' AND [object_id] = OBJECT_ID(N'[dbo].[OrganisationAssignments]'))
                CREATE INDEX [IX_OrganisationAssignments_OrganisationId] ON [dbo].[OrganisationAssignments] ([OrganisationId]);
            """, ct);

    private static Task EnsureDomainRbacTablesAsync(AdviserDirectoryDbContext db, CancellationToken ct)
        => db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[DomainRoles]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[DomainRoles] (
                    [Id] uniqueidentifier NOT NULL,
                    [Role] nvarchar(100) NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_DomainRoles] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID(N'[dbo].[DomainUserRoleMappings]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[DomainUserRoleMappings] (
                    [Id] uniqueidentifier NOT NULL,
                    [RoleId] uniqueidentifier NOT NULL,
                    [Email] nvarchar(320) NULL,
                    [ExternalRole] nvarchar(100) NULL,
                    [ExternalGroupId] nvarchar(128) NULL,
                    [IsEnabled] bit NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    [UpdatedUtc] datetime2 NULL,
                    CONSTRAINT [PK_DomainUserRoleMappings] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID(N'[dbo].[DomainRolePermissions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[DomainRolePermissions] (
                    [Id] uniqueidentifier NOT NULL,
                    [RoleId] uniqueidentifier NOT NULL,
                    [Permission] nvarchar(128) NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_DomainRolePermissions] PRIMARY KEY ([Id])
                );
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainRoles_Role' AND [object_id] = OBJECT_ID(N'[dbo].[DomainRoles]'))
                CREATE UNIQUE INDEX [IX_DomainRoles_Role] ON [dbo].[DomainRoles] ([Role]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_Email' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
                CREATE INDEX [IX_DomainUserRoleMappings_Email] ON [dbo].[DomainUserRoleMappings] ([Email]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_ExternalRole' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
                CREATE INDEX [IX_DomainUserRoleMappings_ExternalRole] ON [dbo].[DomainUserRoleMappings] ([ExternalRole]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_ExternalGroupId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
                CREATE INDEX [IX_DomainUserRoleMappings_ExternalGroupId] ON [dbo].[DomainUserRoleMappings] ([ExternalGroupId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_RoleId_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
                CREATE INDEX [IX_DomainUserRoleMappings_RoleId_IsEnabled] ON [dbo].[DomainUserRoleMappings] ([RoleId], [IsEnabled]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainRolePermissions_RoleId_Permission' AND [object_id] = OBJECT_ID(N'[dbo].[DomainRolePermissions]'))
                CREATE UNIQUE INDEX [IX_DomainRolePermissions_RoleId_Permission] ON [dbo].[DomainRolePermissions] ([RoleId], [Permission]);
            """, ct);

    private static async Task SeedDomainRbacAsync(AdviserDirectoryDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var roleNames = new[] { "Adviser", "LeadTech", "Manager", "Operations", "Admin" };
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

        var roles = await db.DomainRoles.ToDictionaryAsync(x => x.Role, x => x.Id, StringComparer.OrdinalIgnoreCase, ct);
        var permissionsByRole = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
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
                BookingPermissionNames.ApprovalsReview
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
                BookingPermissionNames.CancelAsLeadTech,
                BookingPermissionNames.RearrangeAsLeadTech,
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
                BookingPermissionNames.CancelAsLeadTech,
                BookingPermissionNames.RearrangeAsLeadTech,
                BookingPermissionNames.RearrangementOptionsRead,
                BookingPermissionNames.AdminRead
            ]
        };

        foreach (var (role, permissions) in permissionsByRole)
        {
            if (!roles.TryGetValue(role, out var roleId))
                continue;

            foreach (var permission in permissions)
            {
                if (!await db.DomainRolePermissions.AnyAsync(x => x.RoleId == roleId && x.Permission == permission, ct))
                {
                    db.DomainRolePermissions.Add(new DomainRolePermissionEntity
                    {
                        Id = Guid.NewGuid(),
                        RoleId = roleId,
                        Permission = permission,
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
}
