SET NOCOUNT ON;

DECLARE @now datetime2 = SYSUTCDATETIME();

IF OBJECT_ID(N'[dbo].[DomainRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DomainRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Role] nvarchar(100) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DomainRoles] PRIMARY KEY ([Id])
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

IF OBJECT_ID(N'[dbo].[DomainUserProfiles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DomainUserProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [ExternalSubject] nvarchar(160) NOT NULL,
        [Email] nvarchar(320) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [AdviserId] nvarchar(100) NULL,
        [JobRole] nvarchar(100) NULL,
        [Status] nvarchar(40) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NULL,
        CONSTRAINT [PK_DomainUserProfiles] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DomainUserPermissionMappings] (
        [Id] uniqueidentifier NOT NULL,
        [UserProfileId] uniqueidentifier NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [ExternalSubject] nvarchar(160) NULL,
        [Email] nvarchar(320) NULL,
        [IsGranted] bit NOT NULL,
        [IsEnabled] bit NOT NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NULL,
        CONSTRAINT [PK_DomainUserPermissionMappings] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID(N'[dbo].[DomainUserRoleMappings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DomainUserRoleMappings] (
        [Id] uniqueidentifier NOT NULL,
        [UserProfileId] uniqueidentifier NULL,
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

IF OBJECT_ID(N'[dbo].[DomainAccessScopes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DomainAccessScopes] (
        [Id] uniqueidentifier NOT NULL,
        [Area] nvarchar(80) NOT NULL,
        [ScopeType] nvarchar(80) NOT NULL,
        [ScopeValue] nvarchar(200) NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NULL,
        CONSTRAINT [PK_DomainAccessScopes] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID(N'[dbo].[DomainUserAccessScopeMappings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DomainUserAccessScopeMappings] (
        [Id] uniqueidentifier NOT NULL,
        [UserProfileId] uniqueidentifier NULL,
        [AccessScopeId] uniqueidentifier NOT NULL,
        [ExternalSubject] nvarchar(160) NULL,
        [Email] nvarchar(320) NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NULL,
        CONSTRAINT [PK_DomainUserAccessScopeMappings] PRIMARY KEY ([Id])
    );
END

IF COL_LENGTH(N'[dbo].[DomainUserRoleMappings]', N'UserProfileId') IS NULL
    ALTER TABLE [dbo].[DomainUserRoleMappings] ADD [UserProfileId] uniqueidentifier NULL;

IF COL_LENGTH(N'[dbo].[DomainUserProfiles]', N'JobRole') IS NULL
    ALTER TABLE [dbo].[DomainUserProfiles] ADD [JobRole] nvarchar(100) NULL;

IF COL_LENGTH(N'[dbo].[DomainRolePermissions]', N'PermissionId') IS NULL
    ALTER TABLE [dbo].[DomainRolePermissions] ADD [PermissionId] uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainRoles_Role' AND [object_id] = OBJECT_ID(N'[dbo].[DomainRoles]'))
    CREATE UNIQUE INDEX [IX_DomainRoles_Role] ON [dbo].[DomainRoles] ([Role]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainPermissions_Permission' AND [object_id] = OBJECT_ID(N'[dbo].[DomainPermissions]'))
    CREATE UNIQUE INDEX [IX_DomainPermissions_Permission] ON [dbo].[DomainPermissions] ([Permission]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainPermissions_Category_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainPermissions]'))
    CREATE INDEX [IX_DomainPermissions_Category_IsEnabled] ON [dbo].[DomainPermissions] ([Category], [IsEnabled]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainRolePermissions_RoleId_PermissionId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainRolePermissions]'))
    CREATE UNIQUE INDEX [IX_DomainRolePermissions_RoleId_PermissionId] ON [dbo].[DomainRolePermissions] ([RoleId], [PermissionId]) WHERE [PermissionId] IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserProfiles_ExternalSubject' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserProfiles]'))
    CREATE UNIQUE INDEX [IX_DomainUserProfiles_ExternalSubject] ON [dbo].[DomainUserProfiles] ([ExternalSubject]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserProfiles_Email' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserProfiles]'))
    CREATE INDEX [IX_DomainUserProfiles_Email] ON [dbo].[DomainUserProfiles] ([Email]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserProfiles_AdviserId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserProfiles]'))
    CREATE INDEX [IX_DomainUserProfiles_AdviserId] ON [dbo].[DomainUserProfiles] ([AdviserId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_Email' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
    CREATE INDEX [IX_DomainUserRoleMappings_Email] ON [dbo].[DomainUserRoleMappings] ([Email]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_ExternalRole' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
    CREATE INDEX [IX_DomainUserRoleMappings_ExternalRole] ON [dbo].[DomainUserRoleMappings] ([ExternalRole]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_ExternalGroupId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
    CREATE INDEX [IX_DomainUserRoleMappings_ExternalGroupId] ON [dbo].[DomainUserRoleMappings] ([ExternalGroupId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_RoleId_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
    CREATE INDEX [IX_DomainUserRoleMappings_RoleId_IsEnabled] ON [dbo].[DomainUserRoleMappings] ([RoleId], [IsEnabled]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserRoleMappings_UserProfileId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserRoleMappings]'))
    CREATE INDEX [IX_DomainUserRoleMappings_UserProfileId] ON [dbo].[DomainUserRoleMappings] ([UserProfileId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_UserProfileId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
    CREATE INDEX [IX_DomainUserPermissionMappings_UserProfileId] ON [dbo].[DomainUserPermissionMappings] ([UserProfileId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_ExternalSubject' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
    CREATE INDEX [IX_DomainUserPermissionMappings_ExternalSubject] ON [dbo].[DomainUserPermissionMappings] ([ExternalSubject]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_Email' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
    CREATE INDEX [IX_DomainUserPermissionMappings_Email] ON [dbo].[DomainUserPermissionMappings] ([Email]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_PermissionId_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
    CREATE INDEX [IX_DomainUserPermissionMappings_PermissionId_IsEnabled] ON [dbo].[DomainUserPermissionMappings] ([PermissionId], [IsEnabled]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_UserProfileId_PermissionId_ExternalSubject_Email' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
    CREATE UNIQUE INDEX [IX_DomainUserPermissionMappings_UserProfileId_PermissionId_ExternalSubject_Email]
    ON [dbo].[DomainUserPermissionMappings] ([UserProfileId], [PermissionId], [ExternalSubject], [Email])
    WHERE [UserProfileId] IS NOT NULL AND [ExternalSubject] IS NOT NULL AND [Email] IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainAccessScopes_Area_ScopeType_ScopeValue' AND [object_id] = OBJECT_ID(N'[dbo].[DomainAccessScopes]'))
    CREATE UNIQUE INDEX [IX_DomainAccessScopes_Area_ScopeType_ScopeValue]
    ON [dbo].[DomainAccessScopes] ([Area], [ScopeType], [ScopeValue]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainAccessScopes_Area_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainAccessScopes]'))
    CREATE INDEX [IX_DomainAccessScopes_Area_IsEnabled] ON [dbo].[DomainAccessScopes] ([Area], [IsEnabled]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserAccessScopeMappings_UserProfileId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserAccessScopeMappings]'))
    CREATE INDEX [IX_DomainUserAccessScopeMappings_UserProfileId] ON [dbo].[DomainUserAccessScopeMappings] ([UserProfileId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserAccessScopeMappings_ExternalSubject' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserAccessScopeMappings]'))
    CREATE INDEX [IX_DomainUserAccessScopeMappings_ExternalSubject] ON [dbo].[DomainUserAccessScopeMappings] ([ExternalSubject]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserAccessScopeMappings_Email' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserAccessScopeMappings]'))
    CREATE INDEX [IX_DomainUserAccessScopeMappings_Email] ON [dbo].[DomainUserAccessScopeMappings] ([Email]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserAccessScopeMappings_AccessScopeId_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserAccessScopeMappings]'))
    CREATE INDEX [IX_DomainUserAccessScopeMappings_AccessScopeId_IsEnabled]
    ON [dbo].[DomainUserAccessScopeMappings] ([AccessScopeId], [IsEnabled]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserAccessScopeMappings_UserProfileId_ExternalSubject_Email_AccessScopeId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserAccessScopeMappings]'))
    CREATE UNIQUE INDEX [IX_DomainUserAccessScopeMappings_UserProfileId_ExternalSubject_Email_AccessScopeId]
    ON [dbo].[DomainUserAccessScopeMappings] ([UserProfileId], [ExternalSubject], [Email], [AccessScopeId]);

IF OBJECT_ID(N'[dbo].[DomainUserAccessScopes]', N'U') IS NOT NULL
BEGIN
    MERGE [dbo].[DomainAccessScopes] AS target
    USING (
        SELECT DISTINCT
            [Area],
            [ScopeType],
            [ScopeValue],
            COALESCE(NULLIF([DisplayName], N''), COALESCE([ScopeValue], [ScopeType])) AS [DisplayName],
            CAST(MAX(CAST([IsEnabled] AS int)) AS bit) AS [IsEnabled],
            MIN([CreatedUtc]) AS [CreatedUtc]
        FROM [dbo].[DomainUserAccessScopes]
        GROUP BY [Area], [ScopeType], [ScopeValue], COALESCE(NULLIF([DisplayName], N''), COALESCE([ScopeValue], [ScopeType]))
    ) AS source
    ON target.[Area] = source.[Area]
        AND target.[ScopeType] = source.[ScopeType]
        AND ISNULL(target.[ScopeValue], N'') = ISNULL(source.[ScopeValue], N'')
    WHEN NOT MATCHED THEN
        INSERT ([Id], [Area], [ScopeType], [ScopeValue], [DisplayName], [IsEnabled], [CreatedUtc])
        VALUES (NEWID(), source.[Area], source.[ScopeType], source.[ScopeValue], source.[DisplayName], source.[IsEnabled], source.[CreatedUtc]);

    INSERT INTO [dbo].[DomainUserAccessScopeMappings]
        ([Id], [UserProfileId], [AccessScopeId], [ExternalSubject], [Email], [IsEnabled], [CreatedUtc], [UpdatedUtc])
    SELECT
        old.[Id],
        old.[UserProfileId],
        scope.[Id],
        old.[ExternalSubject],
        old.[Email],
        old.[IsEnabled],
        old.[CreatedUtc],
        old.[UpdatedUtc]
    FROM [dbo].[DomainUserAccessScopes] old
    INNER JOIN [dbo].[DomainAccessScopes] scope
        ON scope.[Area] = old.[Area]
        AND scope.[ScopeType] = old.[ScopeType]
        AND ISNULL(scope.[ScopeValue], N'') = ISNULL(old.[ScopeValue], N'')
    WHERE NOT EXISTS (
        SELECT 1
        FROM [dbo].[DomainUserAccessScopeMappings] existing
        WHERE existing.[Id] = old.[Id]
    );
END

MERGE [dbo].[DomainRoles] AS target
USING (VALUES
    ('Adviser'),
    ('Approver'),
    ('Partner'),
    ('Manager'),
    ('Operations'),
    ('Admin')
) AS source ([Role])
ON target.[Role] = source.[Role]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Role], [CreatedUtc])
    VALUES (NEWID(), source.[Role], @now);

DECLARE @PermissionCatalogue TABLE (
    [Permission] nvarchar(128),
    [DisplayName] nvarchar(160),
    [Category] nvarchar(100)
);

INSERT INTO @PermissionCatalogue ([Permission], [DisplayName], [Category])
VALUES
('Bookings.ApprovalRequests.Create', 'Create booking approval requests', 'Bookings'),
('Bookings.ApprovalRequests.ReadOwn', 'Read own booking approval requests', 'Bookings'),
('Bookings.Approvals.Read', 'Read booking approvals', 'Bookings'),
('Bookings.Approvals.Review', 'Review booking approvals', 'Bookings'),
('Bookings.Cancel.AsPartner', 'Cancel booking as partner', 'Bookings'),
('Bookings.Cancel.Direct', 'Cancel booking directly', 'Bookings'),
('Bookings.Rearrange.AsPartner', 'Rearrange booking as partner', 'Bookings'),
('Bookings.Rearrange.Direct', 'Rearrange booking directly', 'Bookings'),
('Bookings.RearrangementOptions.Read', 'Read booking rearrangement options', 'Bookings'),
('Bookings.Admin.Read', 'Read booking admin data', 'Bookings'),
('Bookings.Reports.Read', 'Read booking reports', 'Reporting'),
('Notifications.Admin.Read', 'Read notification administration', 'Notifications'),
('Notifications.Admin.Manage', 'Manage notification administration', 'Notifications'),
('System.Admin.Read', 'Read system administration', 'System'),
('System.Admin.Manage', 'Manage system administration', 'System'),
('Dashboard.Read', 'Read dashboard', 'Dashboard'),
('OrganisationAssignments.Read', 'Read organisation assignments', 'OrganisationAssignments'),
('OrganisationAssignments.Create', 'Create organisation assignments', 'OrganisationAssignments'),
('OrganisationAssignments.Update', 'Update organisation assignments', 'OrganisationAssignments'),
('OrganisationAssignments.Disable', 'Disable organisation assignments', 'OrganisationAssignments'),
('OrganisationAssignments.Delete', 'Delete organisation assignments', 'OrganisationAssignments'),
('OrganisationAssignments.Manage', 'Manage organisation assignments', 'OrganisationAssignments'),
('Advisers.Read', 'Read adviser directory', 'Advisers'),
('Advisers.Create', 'Create adviser profiles', 'Advisers'),
('Advisers.Update', 'Update adviser profiles', 'Advisers'),
('Advisers.Disable', 'Disable adviser profiles', 'Advisers'),
('Advisers.Delete', 'Delete adviser profiles', 'Advisers'),
('Advisers.Skills.Read', 'Read adviser skills and licences', 'Advisers'),
('Advisers.Skills.Manage', 'Manage adviser skills and licences', 'Advisers'),
('Advisers.Manage', 'Manage advisers', 'Advisers'),
('Advisers.Specialisms.Manage', 'Manage adviser specialisms', 'Advisers'),
('Calendar.Read', 'Read calendar and availability', 'Calendar'),
('Calendar.Manage', 'Manage calendar and availability', 'Calendar'),
('Calendar.Slots.Override', 'Override calendar slots', 'Calendar'),
('Coverage.Read', 'Read coverage map', 'Coverage'),
('Coverage.Manage', 'Manage coverage regions', 'Coverage'),
('CoverageRegions.Read', 'Read coverage regions', 'Coverage'),
('CoverageRegions.Create', 'Create coverage regions', 'Coverage'),
('CoverageRegions.Update', 'Update coverage regions', 'Coverage'),
('CoverageRegions.Disable', 'Disable coverage regions', 'Coverage'),
('CoverageRegions.Delete', 'Delete coverage regions', 'Coverage'),
('CoverageRegions.AssignAdvisers', 'Assign advisers to coverage regions', 'Coverage'),
('Notifications.Read', 'Read notifications', 'Notifications'),
('Notifications.Templates.Read', 'Read notification templates', 'Notifications'),
('Notifications.Templates.Manage', 'Manage notification templates', 'Notifications'),
('Notifications.Settings.Read', 'Read notification settings', 'Notifications'),
('Notifications.Settings.Manage', 'Manage notification settings', 'Notifications'),
('Reporting.Read', 'Read reports', 'Reporting'),
('Audit.Read', 'Read audit logs', 'Audit'),
('System.Read', 'Read system settings', 'System'),
('System.Manage', 'Manage system settings', 'System'),
('System.Health.Read', 'Read system health', 'System'),
('System.Audit.Read', 'Read system audit', 'System'),
('System.Diagnostics.Read', 'Read system diagnostics', 'System'),
('Users.Read', 'Read users', 'Identity'),
('Rbac.Read', 'Read access control', 'Identity'),
('Rbac.Manage', 'Manage access control', 'Identity'),
('Partners.Read', 'Read partner management', 'Partners');

MERGE [dbo].[DomainPermissions] AS target
USING @PermissionCatalogue AS source
ON target.[Permission] = source.[Permission]
WHEN MATCHED THEN
    UPDATE SET
        [DisplayName] = source.[DisplayName],
        [Category] = source.[Category],
        [IsEnabled] = 1,
        [UpdatedUtc] = @now
WHEN NOT MATCHED THEN
    INSERT ([Id], [Permission], [DisplayName], [Description], [Category], [IsEnabled], [CreatedUtc], [UpdatedUtc])
    VALUES (NEWID(), source.[Permission], source.[DisplayName], NULL, source.[Category], 1, @now, NULL);

DECLARE @RolePermissions TABLE (
    [Role] nvarchar(100),
    [Permission] nvarchar(128)
);

INSERT INTO @RolePermissions ([Role], [Permission])
VALUES
('Adviser', 'Bookings.ApprovalRequests.Create'),
('Adviser', 'Bookings.ApprovalRequests.ReadOwn'),
('Approver', 'OrganisationAssignments.Read'),
('Approver', 'Bookings.Approvals.Read'),
('Approver', 'Bookings.Approvals.Review'),
('Approver', 'Dashboard.Read'),
('Approver', 'Bookings.Admin.Read'),
('Partner', 'OrganisationAssignments.Read'),
('Partner', 'Dashboard.Read'),
('Partner', 'Bookings.Admin.Read'),
('Partner', 'Bookings.Approvals.Read'),
('Partner', 'Bookings.Cancel.AsPartner'),
('Partner', 'Bookings.Rearrange.AsPartner'),
('Partner', 'Bookings.RearrangementOptions.Read'),
('Partner', 'Advisers.Read'),
('Partner', 'Advisers.Skills.Read'),
('Partner', 'Calendar.Read'),
('Partner', 'Coverage.Read'),
('Partner', 'CoverageRegions.Read'),
('Manager', 'OrganisationAssignments.Read'),
('Manager', 'Dashboard.Read'),
('Manager', 'Bookings.Admin.Read'),
('Manager', 'Bookings.Approvals.Read'),
('Manager', 'Bookings.Approvals.Review'),
('Manager', 'Bookings.Cancel.Direct'),
('Manager', 'Bookings.Rearrange.Direct'),
('Manager', 'Advisers.Read'),
('Manager', 'Advisers.Skills.Read'),
('Manager', 'Calendar.Read'),
('Manager', 'Coverage.Read'),
('Manager', 'CoverageRegions.Read'),
('Manager', 'Reporting.Read'),
('Manager', 'Bookings.Reports.Read'),
('Operations', 'OrganisationAssignments.Read'),
('Operations', 'OrganisationAssignments.Create'),
('Operations', 'OrganisationAssignments.Update'),
('Operations', 'OrganisationAssignments.Disable'),
('Operations', 'OrganisationAssignments.Delete'),
('Operations', 'OrganisationAssignments.Manage'),
('Operations', 'Bookings.Approvals.Read'),
('Operations', 'Bookings.Approvals.Review'),
('Operations', 'Bookings.ApprovalRequests.Create'),
('Operations', 'Bookings.ApprovalRequests.ReadOwn'),
('Operations', 'Bookings.Cancel.AsPartner'),
('Operations', 'Bookings.Cancel.Direct'),
('Operations', 'Bookings.Rearrange.AsPartner'),
('Operations', 'Bookings.Rearrange.Direct'),
('Operations', 'Bookings.RearrangementOptions.Read'),
('Operations', 'Bookings.Admin.Read'),
('Operations', 'Dashboard.Read'),
('Operations', 'Advisers.Read'),
('Operations', 'Advisers.Create'),
('Operations', 'Advisers.Update'),
('Operations', 'Advisers.Disable'),
('Operations', 'Advisers.Delete'),
('Operations', 'Advisers.Skills.Read'),
('Operations', 'Advisers.Skills.Manage'),
('Operations', 'Advisers.Manage'),
('Operations', 'Advisers.Specialisms.Manage'),
('Operations', 'Calendar.Read'),
('Operations', 'Calendar.Manage'),
('Operations', 'Calendar.Slots.Override'),
('Operations', 'Coverage.Read'),
('Operations', 'Coverage.Manage'),
('Operations', 'CoverageRegions.Read'),
('Operations', 'CoverageRegions.Create'),
('Operations', 'CoverageRegions.Update'),
('Operations', 'CoverageRegions.Disable'),
('Operations', 'CoverageRegions.Delete'),
('Operations', 'CoverageRegions.AssignAdvisers'),
('Operations', 'Notifications.Admin.Read'),
('Operations', 'Notifications.Admin.Manage'),
('Operations', 'Notifications.Read'),
('Operations', 'Notifications.Templates.Read'),
('Operations', 'Notifications.Templates.Manage'),
('Operations', 'Notifications.Settings.Read'),
('Operations', 'Notifications.Settings.Manage'),
('Operations', 'Reporting.Read'),
('Operations', 'Bookings.Reports.Read'),
('Operations', 'Audit.Read'),
('Operations', 'System.Read'),
('Operations', 'System.Admin.Read'),
('Operations', 'System.Health.Read'),
('Operations', 'System.Audit.Read'),
('Operations', 'System.Diagnostics.Read'),
('Operations', 'Users.Read'),
('Operations', 'Rbac.Read'),
('Operations', 'Partners.Read'),
('Admin', 'OrganisationAssignments.Read'),
('Admin', 'OrganisationAssignments.Create'),
('Admin', 'OrganisationAssignments.Update'),
('Admin', 'OrganisationAssignments.Disable'),
('Admin', 'OrganisationAssignments.Delete'),
('Admin', 'OrganisationAssignments.Manage'),
('Admin', 'Bookings.Approvals.Read'),
('Admin', 'Bookings.Approvals.Review'),
('Admin', 'Bookings.ApprovalRequests.Create'),
('Admin', 'Bookings.ApprovalRequests.ReadOwn'),
('Admin', 'Bookings.Cancel.AsPartner'),
('Admin', 'Bookings.Cancel.Direct'),
('Admin', 'Bookings.Rearrange.AsPartner'),
('Admin', 'Bookings.Rearrange.Direct'),
('Admin', 'Bookings.RearrangementOptions.Read'),
('Admin', 'Bookings.Admin.Read'),
('Admin', 'Dashboard.Read'),
('Admin', 'Advisers.Read'),
('Admin', 'Advisers.Create'),
('Admin', 'Advisers.Update'),
('Admin', 'Advisers.Disable'),
('Admin', 'Advisers.Delete'),
('Admin', 'Advisers.Skills.Read'),
('Admin', 'Advisers.Skills.Manage'),
('Admin', 'Advisers.Manage'),
('Admin', 'Advisers.Specialisms.Manage'),
('Admin', 'Calendar.Read'),
('Admin', 'Calendar.Manage'),
('Admin', 'Calendar.Slots.Override'),
('Admin', 'Coverage.Read'),
('Admin', 'Coverage.Manage'),
('Admin', 'CoverageRegions.Read'),
('Admin', 'CoverageRegions.Create'),
('Admin', 'CoverageRegions.Update'),
('Admin', 'CoverageRegions.Disable'),
('Admin', 'CoverageRegions.Delete'),
('Admin', 'CoverageRegions.AssignAdvisers'),
('Admin', 'Notifications.Admin.Read'),
('Admin', 'Notifications.Admin.Manage'),
('Admin', 'Notifications.Read'),
('Admin', 'Notifications.Templates.Read'),
('Admin', 'Notifications.Templates.Manage'),
('Admin', 'Notifications.Settings.Read'),
('Admin', 'Notifications.Settings.Manage'),
('Admin', 'Reporting.Read'),
('Admin', 'Bookings.Reports.Read'),
('Admin', 'Audit.Read'),
('Admin', 'System.Read'),
('Admin', 'System.Manage'),
('Admin', 'System.Admin.Read'),
('Admin', 'System.Admin.Manage'),
('Admin', 'System.Health.Read'),
('Admin', 'System.Audit.Read'),
('Admin', 'System.Diagnostics.Read'),
('Admin', 'Users.Read'),
('Admin', 'Rbac.Read'),
('Admin', 'Rbac.Manage'),
('Admin', 'Partners.Read');

IF COL_LENGTH(N'[dbo].[DomainRolePermissions]', N'Permission') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE rp
        SET [PermissionId] = p.[Id]
        FROM [dbo].[DomainRolePermissions] rp
        JOIN [dbo].[DomainPermissions] p ON p.[Permission] = rp.[Permission]
        WHERE rp.[PermissionId] IS NULL;';
END

INSERT INTO [dbo].[DomainRolePermissions]
    ([Id], [RoleId], [PermissionId], [CreatedUtc])
SELECT NEWID(), r.[Id], p.[Id], @now
FROM @RolePermissions rp
JOIN [dbo].[DomainRoles] r ON r.[Role] = rp.[Role]
JOIN [dbo].[DomainPermissions] p ON p.[Permission] = rp.[Permission]
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[DomainRolePermissions] existing
    WHERE existing.[RoleId] = r.[Id]
      AND existing.[PermissionId] = p.[Id]
);

INSERT INTO [dbo].[DomainUserRoleMappings]
    ([Id], [UserProfileId], [RoleId], [Email], [ExternalRole], [ExternalGroupId], [IsEnabled], [CreatedUtc], [UpdatedUtc])
SELECT NEWID(), NULL, r.[Id], NULL, r.[Role], NULL, 1, @now, NULL
FROM [dbo].[DomainRoles] r
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[DomainUserRoleMappings] m
    WHERE m.[RoleId] = r.[Id]
      AND m.[ExternalRole] = r.[Role]
);

-- Replace these values for local/admin testing.
DECLARE @AdminEmail nvarchar(320) = 'your.email@afh.co.uk';
DECLARE @AdminDisplayName nvarchar(200) = 'Local Admin';

MERGE [dbo].[DomainUserProfiles] AS target
USING (SELECT @AdminEmail AS [Email], @AdminDisplayName AS [DisplayName]) AS source
ON target.[Email] = source.[Email]
WHEN MATCHED THEN
    UPDATE SET
        [DisplayName] = source.[DisplayName],
        [JobRole] = 'Administrator',
        [Status] = 'Active',
        [UpdatedUtc] = @now
WHEN NOT MATCHED THEN
    INSERT ([Id], [ExternalSubject], [Email], [DisplayName], [AdviserId], [JobRole], [Status], [CreatedUtc], [UpdatedUtc])
    VALUES (NEWID(), source.[Email], source.[Email], source.[DisplayName], NULL, 'Administrator', 'Active', @now, NULL);

INSERT INTO [dbo].[DomainUserRoleMappings]
    ([Id], [UserProfileId], [RoleId], [Email], [ExternalRole], [ExternalGroupId], [IsEnabled], [CreatedUtc], [UpdatedUtc])
SELECT NEWID(), p.[Id], r.[Id], p.[Email], NULL, NULL, 1, @now, NULL
FROM [dbo].[DomainUserProfiles] p
JOIN [dbo].[DomainRoles] r ON r.[Role] = 'Admin'
WHERE p.[Email] = @AdminEmail
AND NOT EXISTS (
    SELECT 1
    FROM [dbo].[DomainUserRoleMappings] m
    WHERE m.[UserProfileId] = p.[Id]
      AND m.[RoleId] = r.[Id]
);

DECLARE @AdviserProfiles TABLE (
    [Email] nvarchar(320),
    [DisplayName] nvarchar(200),
    [AdviserId] nvarchar(100),
    [JobRole] nvarchar(100),
    [Role] nvarchar(100)
);

-- Optional adviser profile mappings. AdviserId must match the adviser id used by Booking.
-- JobRole is the business/job title shown on /me; Role is the RBAC role.
-- INSERT INTO @AdviserProfiles ([Email], [DisplayName], [AdviserId], [JobRole], [Role])
-- VALUES
-- ('adviser@example.com', 'Ava Adviser', 'adv-123', 'Financial Adviser', 'Adviser');

MERGE [dbo].[DomainUserProfiles] AS target
USING @AdviserProfiles AS source
ON target.[Email] = source.[Email]
WHEN MATCHED THEN
    UPDATE SET
        [DisplayName] = source.[DisplayName],
        [AdviserId] = source.[AdviserId],
        [JobRole] = source.[JobRole],
        [Status] = 'Active',
        [UpdatedUtc] = @now
WHEN NOT MATCHED THEN
    INSERT ([Id], [ExternalSubject], [Email], [DisplayName], [AdviserId], [JobRole], [Status], [CreatedUtc], [UpdatedUtc])
    VALUES (NEWID(), source.[Email], source.[Email], source.[DisplayName], source.[AdviserId], source.[JobRole], 'Active', @now, NULL);

INSERT INTO [dbo].[DomainUserRoleMappings]
    ([Id], [UserProfileId], [RoleId], [Email], [ExternalRole], [ExternalGroupId], [IsEnabled], [CreatedUtc], [UpdatedUtc])
SELECT NEWID(), p.[Id], r.[Id], p.[Email], NULL, NULL, 1, @now, NULL
FROM @AdviserProfiles ap
JOIN [dbo].[DomainUserProfiles] p ON p.[Email] = ap.[Email]
JOIN [dbo].[DomainRoles] r ON r.[Role] = ap.[Role]
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[DomainUserRoleMappings] m
    WHERE m.[UserProfileId] = p.[Id]
      AND m.[RoleId] = r.[Id]
);

SELECT [Permission], [DisplayName], [Category], [IsEnabled]
FROM [dbo].[DomainPermissions]
ORDER BY [Category], [Permission];

SELECT r.[Role], p.[Permission]
FROM [dbo].[DomainRolePermissions] rp
JOIN [dbo].[DomainRoles] r ON r.[Id] = rp.[RoleId]
JOIN [dbo].[DomainPermissions] p ON p.[Id] = rp.[PermissionId]
ORDER BY r.[Role], p.[Permission];

SELECT up.[Email], up.[DisplayName], up.[AdviserId], up.[JobRole], up.[Status], r.[Role], m.[ExternalRole], m.[IsEnabled]
FROM [dbo].[DomainUserRoleMappings] m
JOIN [dbo].[DomainRoles] r ON r.[Id] = m.[RoleId]
LEFT JOIN [dbo].[DomainUserProfiles] up ON up.[Id] = m.[UserProfileId]
ORDER BY up.[Email], r.[Role], m.[ExternalRole];
