using AFH.Adviser.Application.Models.OrganisationAssignments;
using AFH.Adviser.Contract.V1.OrganisationAssignments;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Tests;

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

    private static AdviserDirectoryDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AdviserDirectoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AdviserDirectoryDbContext(options);
    }
}
