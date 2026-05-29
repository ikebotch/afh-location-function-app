using AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFH.Location.Infrastructure.Persistence.PolicyStore;

/// <summary>
/// Creates policy tables and optionally seeds initial values from configuration.
/// </summary>
public sealed class LocationPolicyDbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocationPolicyDbInitializer> _logger;

    public LocationPolicyDbInitializer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<LocationPolicyDbInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var autoCreate = _configuration.GetValue<bool?>("LocationSearch:PolicyStore:AutoCreate") ?? true;
        if (!autoCreate) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocationPolicyDbContext>();

        await db.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureBusinessContactsTableAsync(db, cancellationToken);
        await SeedIfEmptyAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static Task EnsureBusinessContactsTableAsync(LocationPolicyDbContext db, CancellationToken ct)
        => db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[BusinessContacts]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[BusinessContacts] (
                    [Id] uniqueidentifier NOT NULL,
                    [Context] nvarchar(100) NOT NULL,
                    [ContactType] nvarchar(100) NOT NULL,
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
                    CONSTRAINT [PK_BusinessContacts] PRIMARY KEY ([Id])
                );
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_BusinessContacts_Context_ContactType_IsEnabled_Priority' AND [object_id] = OBJECT_ID(N'[dbo].[BusinessContacts]'))
                CREATE INDEX [IX_BusinessContacts_Context_ContactType_IsEnabled_Priority] ON [dbo].[BusinessContacts] ([Context], [ContactType], [IsEnabled], [Priority]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_BusinessContacts_Region' AND [object_id] = OBJECT_ID(N'[dbo].[BusinessContacts]'))
                CREATE INDEX [IX_BusinessContacts_Region] ON [dbo].[BusinessContacts] ([Region]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_BusinessContacts_AdviserId' AND [object_id] = OBJECT_ID(N'[dbo].[BusinessContacts]'))
                CREATE INDEX [IX_BusinessContacts_AdviserId] ON [dbo].[BusinessContacts] ([AdviserId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_BusinessContacts_ClientId' AND [object_id] = OBJECT_ID(N'[dbo].[BusinessContacts]'))
                CREATE INDEX [IX_BusinessContacts_ClientId] ON [dbo].[BusinessContacts] ([ClientId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_BusinessContacts_OrganisationId' AND [object_id] = OBJECT_ID(N'[dbo].[BusinessContacts]'))
                CREATE INDEX [IX_BusinessContacts_OrganisationId] ON [dbo].[BusinessContacts] ([OrganisationId]);
            """, ct);

    private async Task SeedIfEmptyAsync(LocationPolicyDbContext db, CancellationToken ct)
    {
        var seedFromConfig = _configuration.GetValue<bool?>("LocationSearch:PolicyStore:SeedFromConfiguration") ?? true;
        if (!seedFromConfig) return;

        if (!await db.CoverageDefaults.AnyAsync(ct))
        {
            db.CoverageDefaults.Add(new CoverageDefaultPolicyEntity
            {
                DefaultRadiusMiles = _configuration.GetValue<double?>("LocationSearch:Coverage:DefaultRadiusMiles") ?? 100,
                DefaultMaxTravelTimeMinutes = Math.Max(1, _configuration.GetValue<int?>("LocationSearch:Coverage:DefaultMaxTravelTimeMinutes") ?? 90)
            });
        }

        if (!await db.CoverageRegions.AnyAsync(ct))
        {
            foreach (var child in _configuration.GetSection("LocationSearch:Coverage:RegionRadiusMiles").GetChildren())
            {
                if (string.IsNullOrWhiteSpace(child.Key)) continue;
                if (!double.TryParse(child.Value, out var radius)) continue;

                db.CoverageRegions.Add(new CoverageRegionPolicyEntity
                {
                    Region = child.Key,
                    RadiusMiles = radius
                });
            }

            foreach (var child in _configuration.GetSection("LocationSearch:Coverage:RegionMaxTravelTimeMinutes").GetChildren())
            {
                if (string.IsNullOrWhiteSpace(child.Key)) continue;
                if (!int.TryParse(child.Value, out var maxTravel) || maxTravel <= 0) continue;

                var existing = db.CoverageRegions.Local.FirstOrDefault(x =>
                    string.Equals(x.Region, child.Key, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    db.CoverageRegions.Add(new CoverageRegionPolicyEntity
                    {
                        Region = child.Key,
                        MaxTravelTimeMinutes = maxTravel
                    });
                }
                else
                {
                    existing.MaxTravelTimeMinutes = maxTravel;
                }
            }
        }

        if (!await db.CoverageAdvisers.AnyAsync(ct))
        {
            foreach (var child in _configuration.GetSection("LocationSearch:Coverage:AdviserRadiusMiles").GetChildren())
            {
                if (string.IsNullOrWhiteSpace(child.Key)) continue;
                if (!double.TryParse(child.Value, out var radius)) continue;

                db.CoverageAdvisers.Add(new CoverageAdviserPolicyEntity
                {
                    AdviserId = child.Key,
                    RadiusMiles = radius
                });
            }

            foreach (var child in _configuration.GetSection("LocationSearch:Coverage:AdviserMaxTravelTimeMinutes").GetChildren())
            {
                if (string.IsNullOrWhiteSpace(child.Key)) continue;
                if (!int.TryParse(child.Value, out var maxTravel) || maxTravel <= 0) continue;

                var existing = db.CoverageAdvisers.Local.FirstOrDefault(x =>
                    string.Equals(x.AdviserId, child.Key, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    db.CoverageAdvisers.Add(new CoverageAdviserPolicyEntity
                    {
                        AdviserId = child.Key,
                        MaxTravelTimeMinutes = maxTravel
                    });
                }
                else
                {
                    existing.MaxTravelTimeMinutes = maxTravel;
                }
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Location policy store created/seeded successfully.");
        }
    }
}
