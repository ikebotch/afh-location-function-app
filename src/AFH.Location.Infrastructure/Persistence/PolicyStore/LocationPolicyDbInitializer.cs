using AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;
using AFH.Location.Application.Models.Auth;
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
        await EnsureDomainRbacTablesAsync(db, cancellationToken);
        await SeedDomainRbacAsync(db, cancellationToken);
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

    private static Task EnsureDomainRbacTablesAsync(LocationPolicyDbContext db, CancellationToken ct)
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

    private static async Task SeedDomainRbacAsync(LocationPolicyDbContext db, CancellationToken ct)
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
            ["LeadTech"] = [BusinessContactPermissions.Read],
            ["Manager"] = [BusinessContactPermissions.Read],
            ["Operations"] =
            [
                BusinessContactPermissions.Read,
                BusinessContactPermissions.Create,
                BusinessContactPermissions.Update,
                BusinessContactPermissions.Disable,
                BusinessContactPermissions.Delete
            ],
            ["Admin"] =
            [
                BusinessContactPermissions.Read,
                BusinessContactPermissions.Create,
                BusinessContactPermissions.Update,
                BusinessContactPermissions.Disable,
                BusinessContactPermissions.Delete
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
