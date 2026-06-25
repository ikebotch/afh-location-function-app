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
        await EnsureCoverageRegionTablesAsync(db, cancellationToken);
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

    private static Task EnsureCoverageRegionTablesAsync(AdviserDirectoryDbContext db, CancellationToken ct)
        => db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[CoverageRegions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CoverageRegions] (
                    [Id] uniqueidentifier NOT NULL,
                    [Code] nvarchar(32) NOT NULL,
                    [Name] nvarchar(160) NOT NULL,
                    [LeadAdviserId] nvarchar(100) NULL,
                    [LeadAdviserName] nvarchar(200) NULL,
                    [Postcodes] nvarchar(2000) NOT NULL,
                    [Skills] nvarchar(2000) NOT NULL,
                    [CoverageRadiusMiles] float NOT NULL,
                    [MaxTravelTimeMinutes] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    [UpdatedUtc] datetime2 NULL,
                    CONSTRAINT [PK_CoverageRegions] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID(N'[dbo].[AdviserRegionAssignments]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AdviserRegionAssignments] (
                    [Id] uniqueidentifier NOT NULL,
                    [RegionId] uniqueidentifier NOT NULL,
                    [AdviserId] nvarchar(100) NOT NULL,
                    [AdviserName] nvarchar(200) NOT NULL,
                    [Role] nvarchar(100) NULL,
                    [IsLead] bit NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    [UpdatedUtc] datetime2 NULL,
                    CONSTRAINT [PK_AdviserRegionAssignments] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_AdviserRegionAssignments_CoverageRegions_RegionId] FOREIGN KEY ([RegionId]) REFERENCES [dbo].[CoverageRegions] ([Id]) ON DELETE CASCADE
                );
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CoverageRegions_Code' AND [object_id] = OBJECT_ID(N'[dbo].[CoverageRegions]'))
                CREATE UNIQUE INDEX [IX_CoverageRegions_Code] ON [dbo].[CoverageRegions] ([Code]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CoverageRegions_IsActive_Name' AND [object_id] = OBJECT_ID(N'[dbo].[CoverageRegions]'))
                CREATE INDEX [IX_CoverageRegions_IsActive_Name] ON [dbo].[CoverageRegions] ([IsActive], [Name]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AdviserRegionAssignments_RegionId_AdviserId' AND [object_id] = OBJECT_ID(N'[dbo].[AdviserRegionAssignments]'))
                CREATE UNIQUE INDEX [IX_AdviserRegionAssignments_RegionId_AdviserId] ON [dbo].[AdviserRegionAssignments] ([RegionId], [AdviserId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AdviserRegionAssignments_AdviserId_IsActive' AND [object_id] = OBJECT_ID(N'[dbo].[AdviserRegionAssignments]'))
                CREATE INDEX [IX_AdviserRegionAssignments_AdviserId_IsActive] ON [dbo].[AdviserRegionAssignments] ([AdviserId], [IsActive]);
            """, ct);

}
