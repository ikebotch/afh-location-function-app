SET NOCOUNT ON;

DECLARE @now datetime2 = SYSUTCDATETIME();

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

IF COL_LENGTH(N'[dbo].[DomainUserPermissionMappings]', N'PermissionId') IS NULL
    ALTER TABLE [dbo].[DomainUserPermissionMappings] ADD [PermissionId] uniqueidentifier NULL;

IF COL_LENGTH(N'[dbo].[DomainRolePermissions]', N'PermissionId') IS NULL
    ALTER TABLE [dbo].[DomainRolePermissions] ADD [PermissionId] uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainPermissions_Permission' AND [object_id] = OBJECT_ID(N'[dbo].[DomainPermissions]'))
    CREATE UNIQUE INDEX [IX_DomainPermissions_Permission] ON [dbo].[DomainPermissions] ([Permission]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainPermissions_Category_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainPermissions]'))
    CREATE INDEX [IX_DomainPermissions_Category_IsEnabled] ON [dbo].[DomainPermissions] ([Category], [IsEnabled]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_Email' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
    CREATE INDEX [IX_DomainUserPermissionMappings_Email] ON [dbo].[DomainUserPermissionMappings] ([Email]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_ExternalRole' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
    CREATE INDEX [IX_DomainUserPermissionMappings_ExternalRole] ON [dbo].[DomainUserPermissionMappings] ([ExternalRole]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_ExternalGroupId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
    CREATE INDEX [IX_DomainUserPermissionMappings_ExternalGroupId] ON [dbo].[DomainUserPermissionMappings] ([ExternalGroupId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainUserPermissionMappings_PermissionId_IsEnabled' AND [object_id] = OBJECT_ID(N'[dbo].[DomainUserPermissionMappings]'))
    CREATE INDEX [IX_DomainUserPermissionMappings_PermissionId_IsEnabled] ON [dbo].[DomainUserPermissionMappings] ([PermissionId], [IsEnabled]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DomainRolePermissions_RoleId_PermissionId' AND [object_id] = OBJECT_ID(N'[dbo].[DomainRolePermissions]'))
    CREATE UNIQUE INDEX [IX_DomainRolePermissions_RoleId_PermissionId] ON [dbo].[DomainRolePermissions] ([RoleId], [PermissionId]) WHERE [PermissionId] IS NOT NULL;

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
    ([Id], [RoleId], [Email], [ExternalRole], [ExternalGroupId], [IsEnabled], [CreatedUtc], [UpdatedUtc])
SELECT NEWID(), r.[Id], NULL, r.[Role], NULL, 1, @now, NULL
FROM [dbo].[DomainRoles] r
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[DomainUserRoleMappings] m
    WHERE m.[RoleId] = r.[Id]
      AND m.[ExternalRole] = r.[Role]
);

-- Replace with your actual email for local/admin testing.
DECLARE @AdminEmail nvarchar(256) = 'your.email@afh.co.uk';

INSERT INTO [dbo].[DomainUserRoleMappings]
    ([Id], [RoleId], [Email], [ExternalRole], [ExternalGroupId], [IsEnabled], [CreatedUtc], [UpdatedUtc])
SELECT NEWID(), r.[Id], @AdminEmail, NULL, NULL, 1, @now, NULL
FROM [dbo].[DomainRoles] r
WHERE r.[Role] = 'Admin'
AND NOT EXISTS (
    SELECT 1
    FROM [dbo].[DomainUserRoleMappings] m
    WHERE m.[Email] = @AdminEmail
      AND m.[RoleId] = r.[Id]
);

DECLARE @UserPermissionMappings TABLE (
    [Email] nvarchar(320),
    [Permission] nvarchar(128)
);

-- Optional direct per-user permission grants.
-- INSERT INTO @UserPermissionMappings ([Email], [Permission])
-- VALUES
-- ('adviser@example.com', 'Bookings.ApprovalRequests.Create');

IF COL_LENGTH(N'[dbo].[DomainUserPermissionMappings]', N'Permission') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE up
        SET [PermissionId] = p.[Id]
        FROM [dbo].[DomainUserPermissionMappings] up
        JOIN [dbo].[DomainPermissions] p ON p.[Permission] = up.[Permission]
        WHERE up.[PermissionId] IS NULL;';
END

INSERT INTO [dbo].[DomainUserPermissionMappings]
    ([Id], [PermissionId], [Email], [ExternalRole], [ExternalGroupId], [IsEnabled], [CreatedUtc], [UpdatedUtc])
SELECT NEWID(), p.[Id], up.[Email], NULL, NULL, 1, @now, NULL
FROM @UserPermissionMappings up
JOIN [dbo].[DomainPermissions] p ON p.[Permission] = up.[Permission]
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[DomainUserPermissionMappings] m
    WHERE m.[Email] = up.[Email]
      AND m.[PermissionId] = p.[Id]
);

SELECT [Permission], [DisplayName], [Category], [IsEnabled]
FROM [dbo].[DomainPermissions]
ORDER BY [Category], [Permission];

SELECT r.[Role], p.[Permission]
FROM [dbo].[DomainRolePermissions] rp
JOIN [dbo].[DomainRoles] r ON r.[Id] = rp.[RoleId]
JOIN [dbo].[DomainPermissions] p ON p.[Id] = rp.[PermissionId]
ORDER BY r.[Role], p.[Permission];

SELECT r.[Role], m.[Email], m.[ExternalRole], m.[IsEnabled]
FROM [dbo].[DomainUserRoleMappings] m
JOIN [dbo].[DomainRoles] r ON r.[Id] = m.[RoleId]
ORDER BY r.[Role], m.[Email], m.[ExternalRole];

SELECT m.[Email], m.[ExternalRole], m.[ExternalGroupId], p.[Permission], m.[IsEnabled]
FROM [dbo].[DomainUserPermissionMappings] m
JOIN [dbo].[DomainPermissions] p ON p.[Id] = m.[PermissionId]
ORDER BY m.[Email], m.[ExternalRole], m.[ExternalGroupId], p.[Permission];
