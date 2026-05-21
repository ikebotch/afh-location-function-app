using AFH.Adviser.Domain.Entities;
using AFH.Adviser.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using AFH.Adviser.Application.Abstractions.Repositories;
using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Tests;

public class CachedAdviserRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_UsesCacheBeforeLiveSource()
    {
        var cache = new InMemoryAdviserReferenceCacheRepository();
        await cache.UpsertAsync([
            new Entities.Adviser
            {
                AdviserId = "adv-1",
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
            CreateRefreshCoordinator(cache, source),
            NullLogger<CachedAdviserRepository>.Instance);

        var advisers = await sut.GetAllAsync(null, CancellationToken.None);

        Assert.Single(advisers);
        Assert.Equal("Cached Adviser", advisers[0].DisplayName);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public async Task GetAllAsync_CoalescesConcurrentFullCacheMisses()
    {
        var cache = new InMemoryAdviserReferenceCacheRepository();
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
            CreateRefreshCoordinator(cache, source),
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
        IAdviserReferenceCacheRepository cache,
        IAdviserSourceRepository source)
    {
        var services = new ServiceCollection();
        services.AddScoped<IAdviserReferenceCacheRepository>(_ => cache);
        services.AddScoped<IAdviserSourceRepository>(_ => source);

        var provider = services.BuildServiceProvider();
        return new AdviserSourceRefreshCoordinator(provider.GetRequiredService<IServiceScopeFactory>());
    }

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
