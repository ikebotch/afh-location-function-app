//using AFH.Adviser.Application.Abstractions.Repositories;
//using AFH.Adviser.Application.Services.Feed;
//using AFH.Adviser.Infrastructure.Persistence.Repositories;
//using AFH.Location.Infrastructure.Persistence.PolicyStore;
//using AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;
//using Microsoft.EntityFrameworkCore;
//using Entities = AFH.Adviser.Domain.Entities;

//namespace AFH.Adviser.Tests;

//public sealed class AdviserFeedServiceCoveragePolicyTests
//{
//    [Fact]
//    public async Task GetCoverageFeedAsync_AppliesOrganisationDefault_WhenNoRegionOrAdviserOverrideExists()
//    {
//        await using var db = CreateDbContext();
//        db.CoverageDefaults.Add(new CoverageDefaultPolicyEntity
//        {
//            DefaultRadiusMiles = 80,
//            DefaultMaxTravelTimeMinutes = 90
//        });
//        await db.SaveChangesAsync();

//        var sut = CreateService(db, new Entities.Adviser
//        {
//            AdviserId = "adv-default",
//            DisplayName = "Default Adviser",
//            Region = "North",
//            IsActive = true
//        });

//        var result = await sut.GetCoverageFeedAsync(CancellationToken.None);

//        var adviser = Assert.Single(result.Advisers);
//        Assert.Equal(80, adviser.RadiusMiles);
//        Assert.Equal(90, adviser.MaxTravelTimeMinutes);
//        Assert.Equal("OrganisationDefault", adviser.RadiusSource);
//    }

//    [Fact]
//    public async Task GetCoverageFeedAsync_AppliesRegionOverride_OverOrganisationDefault()
//    {
//        await using var db = CreateDbContext();
//        db.CoverageDefaults.Add(new CoverageDefaultPolicyEntity
//        {
//            DefaultRadiusMiles = 80,
//            DefaultMaxTravelTimeMinutes = 90
//        });
//        db.CoverageRegions.Add(new CoverageRegionPolicyEntity
//        {
//            Region = "North",
//            RadiusMiles = 50,
//            MaxTravelTimeMinutes = 60
//        });
//        await db.SaveChangesAsync();

//        var sut = CreateService(db, new Entities.Adviser
//        {
//            AdviserId = "adv-region",
//            DisplayName = "Region Adviser",
//            Region = "north",
//            IsActive = true
//        });

//        var result = await sut.GetCoverageFeedAsync(CancellationToken.None);

//        var adviser = Assert.Single(result.Advisers);
//        Assert.Equal(50, adviser.RadiusMiles);
//        Assert.Equal(60, adviser.MaxTravelTimeMinutes);
//        Assert.Equal("Region", adviser.RadiusSource);
//    }

//    [Fact]
//    public async Task GetCoverageFeedAsync_AppliesAdviserOverride_OverRegionAndOrganisationDefault()
//    {
//        await using var db = CreateDbContext();
//        db.CoverageDefaults.Add(new CoverageDefaultPolicyEntity
//        {
//            DefaultRadiusMiles = 80,
//            DefaultMaxTravelTimeMinutes = 90
//        });
//        db.CoverageRegions.Add(new CoverageRegionPolicyEntity
//        {
//            Region = "North",
//            RadiusMiles = 50,
//            MaxTravelTimeMinutes = 60
//        });
//        db.CoverageAdvisers.Add(new CoverageAdviserPolicyEntity
//        {
//            AdviserId = "adv-adviser",
//            RadiusMiles = 25,
//            MaxTravelTimeMinutes = 35
//        });
//        await db.SaveChangesAsync();

//        var sut = CreateService(db, new Entities.Adviser
//        {
//            AdviserId = "adv-adviser",
//            DisplayName = "Adviser Override",
//            Region = "North",
//            IsActive = true
//        });

//        var result = await sut.GetCoverageFeedAsync(CancellationToken.None);

//        var adviser = Assert.Single(result.Advisers);
//        Assert.Equal(25, adviser.RadiusMiles);
//        Assert.Equal(35, adviser.MaxTravelTimeMinutes);
//        Assert.Equal("Adviser", adviser.RadiusSource);
//    }

//    [Fact]
//    public async Task GetCoverageFeedAsync_PreservesExistingAdviserValues_AsAdviserLevelOverrides()
//    {
//        await using var db = CreateDbContext();
//        db.CoverageDefaults.Add(new CoverageDefaultPolicyEntity
//        {
//            DefaultRadiusMiles = 80,
//            DefaultMaxTravelTimeMinutes = 90
//        });
//        await db.SaveChangesAsync();

//        var sut = CreateService(db, new Entities.Adviser
//        {
//            AdviserId = "adv-existing",
//            DisplayName = "Existing Adviser",
//            Region = "North",
//            IsActive = true,
//            CoverageRadiusMiles = 20,
//            MaxTravelTimeMinutes = 30
//        });

//        var result = await sut.GetCoverageFeedAsync(CancellationToken.None);

//        var adviser = Assert.Single(result.Advisers);
//        Assert.Equal(20, adviser.RadiusMiles);
//        Assert.Equal(30, adviser.MaxTravelTimeMinutes);
//        Assert.Equal("Adviser", adviser.RadiusSource);
//    }

//    private static AdviserFeedService CreateService(
//        LocationPolicyDbContext db,
//        params Entities.Adviser[] advisers)
//    {
//        return new AdviserFeedService(
//            new StubAdviserRepository(advisers),
//            new SqlEffectiveCoveragePolicyResolver(db));
//    }

//    private static LocationPolicyDbContext CreateDbContext()
//    {
//        var options = new DbContextOptionsBuilder<LocationPolicyDbContext>()
//            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
//            .Options;

//        return new LocationPolicyDbContext(options);
//    }

//    private sealed class StubAdviserRepository : IAdviserRepository
//    {
//        private readonly IReadOnlyList<Entities.Adviser> _advisers;

//        public StubAdviserRepository(IReadOnlyList<Entities.Adviser> advisers)
//        {
//            _advisers = advisers;
//        }

//        public Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(
//            IReadOnlyCollection<string>? adviserIds,
//            CancellationToken ct)
//        {
//            if (adviserIds is null || adviserIds.Count == 0)
//                return Task.FromResult(_advisers);

//            var filtered = _advisers
//                .Where(x => adviserIds.Contains(x.AdviserId, StringComparer.OrdinalIgnoreCase))
//                .ToList();

//            return Task.FromResult<IReadOnlyList<Entities.Adviser>>(filtered);
//        }
//    }
//}