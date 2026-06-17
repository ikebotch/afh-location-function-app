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

}
