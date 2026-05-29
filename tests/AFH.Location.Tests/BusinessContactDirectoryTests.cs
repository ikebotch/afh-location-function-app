using AFH.Location.Application.Models.BusinessContacts;
using AFH.Location.Contract.V1.BusinessContacts;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Tests;

public sealed class BusinessContactDirectoryTests
{
    [Fact]
    public async Task SearchAsync_ReturnsEnabledContactsByNeutralRoleTerms()
    {
        await using var db = CreateDb();
        var directory = new SqlBusinessContactDirectory(db);
        await directory.CreateAsync(new BusinessContactUpsert(
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
        await directory.CreateAsync(new BusinessContactUpsert(
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

        var contacts = await directory.SearchAsync(new BusinessContactSearch(
            "Booking",
            ["Manager"],
            null,
            null,
            "North",
            null), CancellationToken.None);

        var contact = Assert.Single(contacts);
        Assert.Equal("Manager", contact.ContactType);
        Assert.Equal("manager@example.com", contact.Email);
        Assert.DoesNotContain("notification", string.Join(' ', typeof(BusinessContactDto).GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recipient", string.Join(' ', typeof(BusinessContactDto).GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dispatch", string.Join(' ', typeof(BusinessContactDto).GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateDisableAndDelete_AreDbDriven()
    {
        await using var db = CreateDb();
        var directory = new SqlBusinessContactDirectory(db);
        var created = await directory.CreateAsync(new BusinessContactUpsert(
            "Booking",
            "ContactCentre",
            null,
            null,
            null,
            null,
            "Contact Centre",
            "contact@example.com",
            null,
            ["Email"],
            IsEnabled: true,
            Priority: 20), CancellationToken.None);

        var updated = await directory.UpdateAsync(created.Id, new BusinessContactUpsert(
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
        var enabled = await directory.SearchAsync(new BusinessContactSearch("Booking", ["ContactCentre"], null, null, null, null), CancellationToken.None);
        Assert.Empty(enabled);

        var disabled = await directory.SearchAsync(new BusinessContactSearch("Booking", ["ContactCentre"], null, null, null, null, IncludeDisabled: true), CancellationToken.None);
        Assert.Single(disabled);

        Assert.True(await directory.DeleteAsync(created.Id, CancellationToken.None));
        Assert.Null(await directory.GetAsync(created.Id, CancellationToken.None));
    }

    private static LocationPolicyDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LocationPolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new LocationPolicyDbContext(options);
    }
}

