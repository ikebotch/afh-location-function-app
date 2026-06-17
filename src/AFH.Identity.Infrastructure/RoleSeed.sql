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
        [Status] nvarchar(40) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NULL,
        CONSTRAINT [PK_DomainUserProfiles] PRIMARY KEY ([Id])
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

IF COL_LENGTH(N'[dbo].[DomainUserRoleMappings]', N'UserProfileId') IS NULL
    ALTER TABLE [dbo].[DomainUserRoleMappings] ADD [UserProfileId] uniqueidentifier NULL;

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

MERGE [dbo].[DomainRoles] AS target
USING (VALUES
    ('Adviser'),
    ('Approver'),
    ('LeadTech'),
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
('Bookings.Cancel.AsLeadTech', 'Cancel booking as lead tech', 'Bookings'),
('Bookings.Cancel.Direct', 'Cancel booking directly', 'Bookings'),
('Bookings.Rearrange.AsLeadTech', 'Rearrange booking as lead tech', 'Bookings'),
('Bookings.Rearrange.Direct', 'Rearrange booking directly', 'Bookings'),
('Bookings.RearrangementOptions.Read', 'Read booking rearrangement options', 'Bookings'),
('Bookings.Admin.Read', 'Read booking admin data', 'Bookings'),
('OrganisationAssignments.Read', 'Read organisation assignments', 'OrganisationAssignments'),
('OrganisationAssignments.Create', 'Create organisation assignments', 'OrganisationAssignments'),
('OrganisationAssignments.Update', 'Update organisation assignments', 'OrganisationAssignments'),
('OrganisationAssignments.Disable', 'Disable organisation assignments', 'OrganisationAssignments'),
('OrganisationAssignments.Delete', 'Delete organisation assignments', 'OrganisationAssignments');

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
('LeadTech', 'OrganisationAssignments.Read'),
('LeadTech', 'Bookings.Approvals.Read'),
('LeadTech', 'Bookings.Cancel.AsLeadTech'),
('LeadTech', 'Bookings.Rearrange.AsLeadTech'),
('LeadTech', 'Bookings.RearrangementOptions.Read'),
('Manager', 'OrganisationAssignments.Read'),
('Manager', 'Bookings.Approvals.Read'),
('Manager', 'Bookings.Approvals.Review'),
('Manager', 'Bookings.Cancel.Direct'),
('Manager', 'Bookings.Rearrange.Direct'),
('Operations', 'OrganisationAssignments.Read'),
('Operations', 'OrganisationAssignments.Create'),
('Operations', 'OrganisationAssignments.Update'),
('Operations', 'OrganisationAssignments.Disable'),
('Operations', 'OrganisationAssignments.Delete'),
('Operations', 'Bookings.Approvals.Read'),
('Operations', 'Bookings.Approvals.Review'),
('Operations', 'Bookings.ApprovalRequests.Create'),
('Operations', 'Bookings.ApprovalRequests.ReadOwn'),
('Operations', 'Bookings.Cancel.AsLeadTech'),
('Operations', 'Bookings.Cancel.Direct'),
('Operations', 'Bookings.Rearrange.AsLeadTech'),
('Operations', 'Bookings.Rearrange.Direct'),
('Operations', 'Bookings.RearrangementOptions.Read'),
('Operations', 'Bookings.Admin.Read'),
('Admin', 'OrganisationAssignments.Read'),
('Admin', 'OrganisationAssignments.Create'),
('Admin', 'OrganisationAssignments.Update'),
('Admin', 'OrganisationAssignments.Disable'),
('Admin', 'OrganisationAssignments.Delete'),
('Admin', 'Bookings.Approvals.Read'),
('Admin', 'Bookings.Approvals.Review'),
('Admin', 'Bookings.ApprovalRequests.Create'),
('Admin', 'Bookings.ApprovalRequests.ReadOwn'),
('Admin', 'Bookings.Cancel.AsLeadTech'),
('Admin', 'Bookings.Cancel.Direct'),
('Admin', 'Bookings.Rearrange.AsLeadTech'),
('Admin', 'Bookings.Rearrange.Direct'),
('Admin', 'Bookings.RearrangementOptions.Read'),
('Admin', 'Bookings.Admin.Read');

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
        [Status] = 'Active',
        [UpdatedUtc] = @now
WHEN NOT MATCHED THEN
    INSERT ([Id], [ExternalSubject], [Email], [DisplayName], [AdviserId], [Status], [CreatedUtc], [UpdatedUtc])
    VALUES (NEWID(), source.[Email], source.[Email], source.[DisplayName], NULL, 'Active', @now, NULL);

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
    [Role] nvarchar(100)
);

-- Optional adviser profile mappings. AdviserId must match the adviser id used by Booking.
-- INSERT INTO @AdviserProfiles ([Email], [DisplayName], [AdviserId], [Role])
-- VALUES
-- ('adviser@example.com', 'Ava Adviser', 'adv-123', 'Adviser');

MERGE [dbo].[DomainUserProfiles] AS target
USING @AdviserProfiles AS source
ON target.[Email] = source.[Email]
WHEN MATCHED THEN
    UPDATE SET
        [DisplayName] = source.[DisplayName],
        [AdviserId] = source.[AdviserId],
        [Status] = 'Active',
        [UpdatedUtc] = @now
WHEN NOT MATCHED THEN
    INSERT ([Id], [ExternalSubject], [Email], [DisplayName], [AdviserId], [Status], [CreatedUtc], [UpdatedUtc])
    VALUES (NEWID(), source.[Email], source.[Email], source.[DisplayName], source.[AdviserId], 'Active', @now, NULL);

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

SELECT up.[Email], up.[DisplayName], up.[AdviserId], up.[Status], r.[Role], m.[ExternalRole], m.[IsEnabled]
FROM [dbo].[DomainUserRoleMappings] m
JOIN [dbo].[DomainRoles] r ON r.[Id] = m.[RoleId]
LEFT JOIN [dbo].[DomainUserProfiles] up ON up.[Id] = m.[UserProfileId]
ORDER BY up.[Email], r.[Role], m.[ExternalRole];
