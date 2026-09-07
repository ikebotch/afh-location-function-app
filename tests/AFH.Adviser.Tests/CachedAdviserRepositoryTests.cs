using AFH.Adviser.Domain.Entities;
using AFH.Adviser.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using AFH.Adviser.Application.Abstractions.Repositories;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using Microsoft.EntityFrameworkCore;
using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Tests;

public class CachedAdviserRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_UsesCacheBeforeLiveSource()
    {
        var options = CreateDbOptions();
        await using var db = CreateDb(options);
        var cache = new SqlAdviserReferenceCacheRepository(db);
        await cache.UpsertAsync([
            new Entities.Adviser
            {
                AdviserId = "adv-1",
                XPlanAdviserId = "987654321",
                DisplayName = "Cached Adviser",
                HomePostcode = "B1 1AA",
                Region = "West Midlands",
                IsActive = true
            }
        ], DateTime.UtcNow, CancellationToken.None);

        var source = new StubAdviserSourceRepository();
        var sut = new CachedAdviserRepository(
            cache,
            source,
            CreateRefreshCoordinator(options, source),
            NullLogger<CachedAdviserRepository>.Instance);

        var advisers = await sut.GetAllAsync(null, CancellationToken.None);

        Assert.Single(advisers);
        Assert.Equal("Cached Adviser", advisers[0].DisplayName);
        Assert.Equal("987654321", advisers[0].XPlanAdviserId);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public async Task GetAllAsync_CoalescesConcurrentFullCacheMisses()
    {
        var options = CreateDbOptions();
        await using var db = CreateDb(options);
        var cache = new SqlAdviserReferenceCacheRepository(db);
        var source = new BlockingAdviserSourceRepository([
            new Entities.Adviser
            {
                AdviserId = "adv-1",
                DisplayName = "Live Adviser",
                HomePostcode = "B1 1AA",
                Region = "West Midlands",
                IsActive = true
            }
        ]);
        var sut = new CachedAdviserRepository(
            cache,
            source,
            CreateRefreshCoordinator(options, source),
            NullLogger<CachedAdviserRepository>.Instance);

        var firstTask = sut.GetAllAsync(null, CancellationToken.None);
        var secondTask = sut.GetAllAsync(null, CancellationToken.None);

        await source.WhenStarted;
        Assert.Equal(1, source.CallCount);

        source.Release();

        var first = await firstTask;
        var second = await secondTask;

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal("Live Adviser", first[0].DisplayName);
        Assert.Equal("Live Adviser", second[0].DisplayName);
        Assert.Equal(1, source.CallCount);

        var cached = await cache.GetAllAsync(null, CancellationToken.None);
        Assert.Single(cached);
        Assert.Equal("Live Adviser", cached[0].DisplayName);
    }

    private static AdviserSourceRefreshCoordinator CreateRefreshCoordinator(
        DbContextOptions<LocationPolicyDbContext> options,
        IAdviserSourceRepository source)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new LocationPolicyDbContext(options));
        services.AddScoped<IAdviserReferenceCacheRepository, SqlAdviserReferenceCacheRepository>();
        services.AddScoped<IAdviserSourceRepository>(_ => source);

        var provider = services.BuildServiceProvider();
        return new AdviserSourceRefreshCoordinator(provider.GetRequiredService<IServiceScopeFactory>());
    }

    private static DbContextOptions<LocationPolicyDbContext> CreateDbOptions()
    {
        return new DbContextOptionsBuilder<LocationPolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
    }

    private static LocationPolicyDbContext CreateDb(DbContextOptions<LocationPolicyDbContext> options)
        => new(options);

    private sealed class StubAdviserSourceRepository : IAdviserSourceRepository
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<Entities.Adviser>>([]);
        }
    }

    private sealed class BlockingAdviserSourceRepository : IAdviserSourceRepository
    {
        private readonly IReadOnlyList<Entities.Adviser> _advisers;
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingAdviserSourceRepository(IReadOnlyList<Entities.Adviser> advisers)
        {
            _advisers = advisers;
        }

        public int CallCount { get; private set; }
        public Task WhenStarted => _started.Task;

        public async Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
        {
            CallCount++;
            _started.TrySetResult();
            await _release.Task;
            return _advisers;
        }

        public void Release() => _release.TrySetResult();
    }
}
