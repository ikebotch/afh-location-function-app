using AFH.Adviser.Application.Models.Skills;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;
using AFH.Adviser.Infrastructure.Persistence.Skills;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Tests;

public sealed class AdviserSkillAdminServiceTests
{
    [Fact]
    public async Task ListAsync_HidesInactiveSkillsUnlessRequested()
    {
        await using var db = CreateDb();
        var sut = new SqlAdviserSkillAdminService(db);

        var active = await sut.UpsertAsync(null, new AdviserSkillUpsert(
            "Equity Release",
            "Later Life Lending",
            "Lifetime mortgages and home reversion advice",
            LicenseRequired: true,
            Certification: "CeRER / FCA",
            RenewalMonths: 12,
            IsActive: true), CancellationToken.None);
        var inactive = await sut.UpsertAsync(null, new AdviserSkillUpsert(
            "Legacy Advice",
            "Archive",
            null,
            LicenseRequired: false,
            Certification: null,
            RenewalMonths: null,
            IsActive: false), CancellationToken.None);

        var visible = await sut.ListAsync(includeInactive: false, CancellationToken.None);
        var all = await sut.ListAsync(includeInactive: true, CancellationToken.None);

        var row = Assert.Single(visible);
        Assert.Equal(active.Id, row.Id);
        Assert.Equal("Equity Release", row.Name);
        Assert.Equal("CeRER / FCA", row.Certification);
        Assert.True(row.LicenseRequired);
        Assert.Equal(12, row.RenewalMonths);
        Assert.Contains(all, x => x.Id == inactive.Id);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingSkillById()
    {
        await using var db = CreateDb();
        var sut = new SqlAdviserSkillAdminService(db);
        var created = await sut.UpsertAsync(null, new AdviserSkillUpsert(
            "Mortgage",
            "Advice",
            null,
            LicenseRequired: true,
            Certification: "CeMAP",
            RenewalMonths: 24,
            IsActive: true), CancellationToken.None);

        var updated = await sut.UpsertAsync(created.Id, new AdviserSkillUpsert(
            "Mortgage Advice",
            "Advice",
            "Residential and buy-to-let mortgage advice",
            LicenseRequired: true,
            Certification: "CeMAP",
            RenewalMonths: 12,
            IsActive: true), CancellationToken.None);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Mortgage Advice", updated.Name);
        Assert.Equal(12, updated.RenewalMonths);
        Assert.Single(await sut.ListAsync(includeInactive: true, CancellationToken.None));
    }

    private static AdviserDirectoryDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AdviserDirectoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AdviserDirectoryDbContext(options);
    }
}
