using AFH.Adviser.Application.Models.Auth;
using AFH.Adviser.Application.Models.OrganisationAssignments;
using AFH.Adviser.Contract.V1.OrganisationAssignments;
using AFH.Location.Application.Models.Auth;
using AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Tests;

public sealed class OrganisationAssignmentDirectoryTests
{
    [Fact]
    public async Task SearchAsync_ReturnsEnabledContactsByNeutralRoleTerms()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await directory.CreateAsync(new OrganisationAssignmentUpsert(
            "Booking",
            "Manager",
            null,
            null,
            "North",
            null,
            "Regional Manager",
            "manager@example.com",
            null,
            ["Email"],
            IsEnabled: true,
            Priority: 10), CancellationToken.None);
        await directory.CreateAsync(new OrganisationAssignmentUpsert(
            "Booking",
            "Manager",
            null,
            null,
            "South",
            null,
            "Disabled Manager",
            "disabled@example.com",
            null,
            ["Email"],
            IsEnabled: false,
            Priority: 1), CancellationToken.None);

        var assignments = await directory.SearchAsync(new OrganisationAssignmentSearch(
            "Booking",
            ["Manager"],
            null,
            null,
            "North",
            null), CancellationToken.None);

        var assignment = Assert.Single(assignments);
        Assert.Equal("Manager", assignment.AssignmentType);
        Assert.Equal("manager@example.com", assignment.Email);
        Assert.DoesNotContain("notification", string.Join(' ', typeof(OrganisationAssignmentDto).GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recipient", string.Join(' ', typeof(OrganisationAssignmentDto).GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dispatch", string.Join(' ', typeof(OrganisationAssignmentDto).GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateDisableAndDelete_AreDbDriven()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        var created = await directory.CreateAsync(new OrganisationAssignmentUpsert(
            "Booking",
            "ContactCentre",
            null,
            null,
            null,
            null,
            "Contact Centre",
            "assignment@example.com",
            null,
            ["Email"],
            IsEnabled: true,
            Priority: 20), CancellationToken.None);

        var updated = await directory.UpdateAsync(created.Id, new OrganisationAssignmentUpsert(
            "Booking",
            "ContactCentre",
            null,
            null,
            null,
            null,
            "Contact Centre",
            "updated@example.com",
            null,
            ["Email"],
            IsEnabled: true,
            Priority: 5), CancellationToken.None);
        Assert.Equal("updated@example.com", updated?.Email);

        Assert.True(await directory.DisableAsync(created.Id, CancellationToken.None));
        var enabled = await directory.SearchAsync(new OrganisationAssignmentSearch("Booking", ["ContactCentre"], null, null, null, null), CancellationToken.None);
        Assert.Empty(enabled);

        var disabled = await directory.SearchAsync(new OrganisationAssignmentSearch("Booking", ["ContactCentre"], null, null, null, null, IncludeDisabled: true), CancellationToken.None);
        Assert.Single(disabled);

        Assert.True(await directory.DeleteAsync(created.Id, CancellationToken.None));
        Assert.Null(await directory.GetAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DomainUserPermissionStore_GrantsReadToManagerFromDbRoleMapping()
    {
        await using var db = CreateDb();
        await SeedRoleAsync(db, "Manager", "Manager", [OrganisationAssignmentPermissions.Read]);
        var store = new SqlDomainUserPermissionStore(db);

        var allowed = await store.HasPermissionAsync(
            new DomainUserIdentity("manager@afh.co.uk", ["Manager"], []),
            OrganisationAssignmentPermissions.Read,
            CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact]
    public async Task DomainUserPermissionStore_DeniesCreateToManager()
    {
        await using var db = CreateDb();
        await SeedRoleAsync(db, "Manager", "Manager", [OrganisationAssignmentPermissions.Read]);
        var store = new SqlDomainUserPermissionStore(db);

        var allowed = await store.HasPermissionAsync(
            new DomainUserIdentity("manager@afh.co.uk", ["Manager"], []),
            OrganisationAssignmentPermissions.Create,
            CancellationToken.None);

        Assert.False(allowed);
    }

    [Fact]
    public async Task DomainUserPermissionStore_GrantsManagementToOperations()
    {
        await using var db = CreateDb();
        await SeedRoleAsync(db, "Operations", "Operations",
        [
            OrganisationAssignmentPermissions.Read,
            OrganisationAssignmentPermissions.Create,
            OrganisationAssignmentPermissions.Update,
            OrganisationAssignmentPermissions.Disable,
            OrganisationAssignmentPermissions.Delete
        ]);
        var store = new SqlDomainUserPermissionStore(db);

        var identity = new DomainUserIdentity("ops@afh.co.uk", ["Operations"], []);

        Assert.True(await store.HasPermissionAsync(identity, OrganisationAssignmentPermissions.Create, CancellationToken.None));
        Assert.True(await store.HasPermissionAsync(identity, OrganisationAssignmentPermissions.Update, CancellationToken.None));
        Assert.True(await store.HasPermissionAsync(identity, OrganisationAssignmentPermissions.Disable, CancellationToken.None));
        Assert.True(await store.HasPermissionAsync(identity, OrganisationAssignmentPermissions.Delete, CancellationToken.None));
    }

    private static async Task SeedRoleAsync(
        LocationPolicyDbContext db,
        string role,
        string externalRole,
        IReadOnlyList<string> permissions)
    {
        var roleId = Guid.NewGuid();
        db.DomainRoles.Add(new DomainRoleEntity { Id = roleId, Role = role, CreatedUtc = DateTime.UtcNow });
        db.DomainUserRoleMappings.Add(new DomainUserRoleMappingEntity
        {
            Id = Guid.NewGuid(),
            RoleId = roleId,
            ExternalRole = externalRole,
            IsEnabled = true,
            CreatedUtc = DateTime.UtcNow
        });
        foreach (var permission in permissions)
        {
            db.DomainRolePermissions.Add(new DomainRolePermissionEntity
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                Permission = permission,
                CreatedUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static LocationPolicyDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LocationPolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new LocationPolicyDbContext(options);
    }
}
