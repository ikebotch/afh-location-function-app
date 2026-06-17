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

            IF OBJECT_ID(N'[dbo].[DomainPermissions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[DomainPermissions] (
                    [Id] uniqueidentifier NOT NULL,
                    [Permission] nvarchar(128) NOT NULL,
                    [DisplayName] nvarchar(160) NOT NULL,
                    [Description] nvarchar(500) NULL,
                    [Category] nvarchar(100) NOT NULL,
                    [IsEnabled] bit NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    [UpdatedUtc] datetime2 NULL,
                    CONSTRAINT [PK_DomainPermissions] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID(N'[dbo].[DomainRolePermissions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[DomainRolePermissions] (
                    [Id] uniqueidentifier NOT NULL,
                    [RoleId] uniqueidentifier NOT NULL,
                    [PermissionId] uniqueidentifier NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_DomainRolePermissions] PRIMARY KEY ([Id])
                );
            END
            ELSE IF COL_LENGTH(N'[dbo].[DomainRolePermissions]', N'PermissionId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[DomainRolePermissions] ADD [PermissionId] uniqueidentifier NULL;
            END

            IF OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[DomainUserPermissionMappings] (
                    [Id] uniqueidentifier NOT NULL,
                    [PermissionId] uniqueidentifier NOT NULL,
                    [Email] nvarchar(320) NULL,
                    [ExternalRole] nvarchar(100) NULL,
                    [ExternalGroupId] nvarchar(128) NULL,
                    [IsEnabled] bit NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    [UpdatedUtc] datetime2 NULL,
                    CONSTRAINT [PK_DomainUserPermissionMappings] PRIMARY KEY ([Id])
                );
            END
            ELSE IF COL_LENGTH(N'[dbo].[DomainUserPermissionMappings]', N'PermissionId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[DomainUserPermissionMappings] ADD [PermissionId] uniqueidentifier NULL;
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainRoles_Role' AND [object_id] = OBJECT_ID(N'[dbo].[DomainRoles]'))
                CREATE UNIQUE INDEX [IX_DomainRoles_Role] ON [dbo].[DomainRoles] ([Role]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainPermissions_Permission' AND [object_id] = OBJECT_ID(N'[dbo].[DomainPermissions]'))
                CREATE UNIQUE INDEX [IX_DomainPermissions_Permission] ON [dbo].[DomainPermissions] ([Permission]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainPermissions_Category_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainPermissions]'))
                CREATE INDEX [IX_DomainPermissions_Category_IsEnabled] ON [dbo].[DomainPermissions] ([Category], [IsEnabled]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_Email' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
                CREATE INDEX [IX_DomainUserRoleMappings_Email] ON [dbo].[DomainUserRoleMappings] ([Email]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_ExternalRole' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
                CREATE INDEX [IX_DomainUserRoleMappings_ExternalRole] ON [dbo].[DomainUserRoleMappings] ([ExternalRole]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_ExternalGroupId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
                CREATE INDEX [IX_DomainUserRoleMappings_ExternalGroupId] ON [dbo].[DomainUserRoleMappings] ([ExternalGroupId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_RoleId_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
                CREATE INDEX [IX_DomainUserRoleMappings_RoleId_IsEnabled] ON [dbo].[DomainUserRoleMappings] ([RoleId], [IsEnabled]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainRolePermissions_RoleId_PermissionId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainRolePermissions]'))
                CREATE UNIQUE INDEX [IX_DomainRolePermissions_RoleId_PermissionId] ON [dbo].[DomainRolePermissions] ([RoleId], [PermissionId]) WHERE [PermissionId] IS NOT NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_Email' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
                CREATE INDEX [IX_DomainUserPermissionMappings_Email] ON [dbo].[DomainUserPermissionMappings] ([Email]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_ExternalRole' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
                CREATE INDEX [IX_DomainUserPermissionMappings_ExternalRole] ON [dbo].[DomainUserPermissionMappings] ([ExternalRole]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_ExternalGroupId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
                CREATE INDEX [IX_DomainUserPermissionMappings_ExternalGroupId] ON [dbo].[DomainUserPermissionMappings] ([ExternalGroupId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_PermissionId_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
                CREATE INDEX [IX_DomainUserPermissionMappings_PermissionId_IsEnabled] ON [dbo].[DomainUserPermissionMappings] ([PermissionId], [IsEnabled]);

            IF COL_LENGTH(N'[dbo].[DomainRolePermissions]', N'Permission') IS NOT NULL
            BEGIN
                EXEC sp_executesql N'
                    INSERT INTO [dbo].[DomainPermissions]
                        ([Id], [Permission], [DisplayName], [Description], [Category], [IsEnabled], [CreatedUtc], [UpdatedUtc])
                    SELECT NEWID(),
                           legacy.[Permission],
                           REPLACE(legacy.[Permission], ''.'', '' ''),
                           NULL,
                           CASE
                               WHEN CHARINDEX(''.'', legacy.[Permission]) > 1 THEN LEFT(legacy.[Permission], CHARINDEX(''.'', legacy.[Permission]) - 1)
                               ELSE ''General''
                           END,
                           1,
                           SYSUTCDATETIME(),
                           NULL
                    FROM (
                        SELECT DISTINCT [Permission]
                        FROM [dbo].[DomainRolePermissions]
                        WHERE [Permission] IS NOT NULL AND LTRIM(RTRIM([Permission])) <> ''''
                    ) legacy
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM [dbo].[DomainPermissions] existing
                        WHERE existing.[Permission] = legacy.[Permission]
                    );

                    UPDATE rp
                    SET [PermissionId] = p.[Id]
                    FROM [dbo].[DomainRolePermissions] rp
                    JOIN [dbo].[DomainPermissions] p ON p.[Permission] = rp.[Permission]
                    WHERE rp.[PermissionId] IS NULL;';
            END

            IF COL_LENGTH(N'[dbo].[DomainUserPermissionMappings]', N'Permission') IS NOT NULL
            BEGIN
                EXEC sp_executesql N'
                    INSERT INTO [dbo].[DomainPermissions]
                        ([Id], [Permission], [DisplayName], [Description], [Category], [IsEnabled], [CreatedUtc], [UpdatedUtc])
                    SELECT NEWID(),
                           legacy.[Permission],
                           REPLACE(legacy.[Permission], ''.'', '' ''),
                           NULL,
                           CASE
                               WHEN CHARINDEX(''.'', legacy.[Permission]) > 1 THEN LEFT(legacy.[Permission], CHARINDEX(''.'', legacy.[Permission]) - 1)
                               ELSE ''General''
                           END,
                           1,
                           SYSUTCDATETIME(),
                           NULL
                    FROM (
                        SELECT DISTINCT [Permission]
                        FROM [dbo].[DomainUserPermissionMappings]
                        WHERE [Permission] IS NOT NULL AND LTRIM(RTRIM([Permission])) <> ''''
                    ) legacy
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM [dbo].[DomainPermissions] existing
                        WHERE existing.[Permission] = legacy.[Permission]
                    );

                    UPDATE up
                    SET [PermissionId] = p.[Id]
                    FROM [dbo].[DomainUserPermissionMappings] up
                    JOIN [dbo].[DomainPermissions] p ON p.[Permission] = up.[Permission]
                    WHERE up.[PermissionId] IS NULL;';
            END
            """, ct);

    private static async Task SeedDomainRbacAsync(AdviserDirectoryDbContext db, CancellationToken ct)
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
