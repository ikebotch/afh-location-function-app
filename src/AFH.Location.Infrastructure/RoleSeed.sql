SET NOCOUNT ON;

DECLARE @now datetime2 = SYSUTCDATETIME();

MERGE [dbo].[DomainRoles] AS target
USING (VALUES
    ('Adviser'),
    ('LeadTech'),
    ('Manager'),
    ('Operations'),
    ('Admin')
) AS source ([Role])
ON target.[Role] = source.[Role]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Role], [CreatedUtc])
    VALUES (NEWID(), source.[Role], @now);

DECLARE @RolePermissions TABLE (
    [Role] nvarchar(100),
    [Permission] nvarchar(128)
);

INSERT INTO @RolePermissions ([Role], [Permission])
VALUES
('LeadTech', 'OrganisationAssignments.Read'),
('LeadTech', 'Bookings.Approvals.Read'),
('LeadTech', 'Bookings.Cancel.AsLeadTech'),
('LeadTech', 'Bookings.Rearrange.AsLeadTech'),
('LeadTech', 'Bookings.RearrangementOptions.Read'),

('Manager', 'OrganisationAssignments.Read'),
('Manager', 'Bookings.Approvals.Read'),
('Manager', 'Bookings.Approvals.Review'),

('Operations', 'OrganisationAssignments.Read'),
('Operations', 'OrganisationAssignments.Create'),
('Operations', 'OrganisationAssignments.Update'),
('Operations', 'OrganisationAssignments.Disable'),
('Operations', 'OrganisationAssignments.Delete'),
('Operations', 'Bookings.Approvals.Read'),
('Operations', 'Bookings.Approvals.Review'),
('Operations', 'Bookings.Cancel.AsLeadTech'),
('Operations', 'Bookings.Rearrange.AsLeadTech'),
('Operations', 'Bookings.RearrangementOptions.Read'),
('Operations', 'Bookings.Admin.Read'),

('Admin', 'OrganisationAssignments.Read'),
('Admin', 'OrganisationAssignments.Create'),
('Admin', 'OrganisationAssignments.Update'),
('Admin', 'OrganisationAssignments.Disable'),
('Admin', 'OrganisationAssignments.Delete'),
('Admin', 'Bookings.Approvals.Read'),
('Admin', 'Bookings.Approvals.Review'),
('Admin', 'Bookings.Cancel.AsLeadTech'),
('Admin', 'Bookings.Rearrange.AsLeadTech'),
('Admin', 'Bookings.RearrangementOptions.Read'),
('Admin', 'Bookings.Admin.Read');

INSERT INTO [dbo].[DomainRolePermissions]
    ([Id], [RoleId], [Permission], [CreatedUtc])
SELECT NEWID(), r.[Id], rp.[Permission], @now
FROM @RolePermissions rp
JOIN [dbo].[DomainRoles] r
    ON r.[Role] = rp.[Role]
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[DomainRolePermissions] existing
    WHERE existing.[RoleId] = r.[Id]
      AND existing.[Permission] = rp.[Permission]
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

SELECT * FROM [dbo].[DomainRoles];
SELECT r.[Role], rp.[Permission]
FROM [dbo].[DomainRolePermissions] rp
JOIN [dbo].[DomainRoles] r ON r.[Id] = rp.[RoleId]
ORDER BY r.[Role], rp.[Permission];

SELECT r.[Role], m.[Email], m.[ExternalRole], m.[IsEnabled]
FROM [dbo].[DomainUserRoleMappings] m
JOIN [dbo].[DomainRoles] r ON r.[Id] = m.[RoleId]
ORDER BY r.[Role], m.[Email], m.[ExternalRole];