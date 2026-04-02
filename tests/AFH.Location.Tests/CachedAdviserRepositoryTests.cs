using AFH.Location.Application.Abstractions;
using AFH.Location.Domain.Entities;
using AFH.Location.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Location.Tests;

public class CachedAdviserRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_UsesCacheBeforeLiveSource()
    {
        var cache = new InMemoryAdviserReferenceCacheRepository();
        await cache.UpsertAsync([
            new Adviser
            {
                AdviserId = "adv-1",
                DisplayName = "Cached Adviser",
                HomePostcode = "B1 1AA",
                Region = "West Midlands",
                IsActive = true
            }
        ], DateTime.UtcNow, CancellationToken.None);

        var source = new StubAdviserSourceRepository();
        var sut = new CachedAdviserRepository(cache, source, NullLogger<CachedAdviserRepository>.Instance);

        var advisers = await sut.GetAllAsync(null, CancellationToken.None);

        Assert.Single(advisers);
        Assert.Equal("Cached Adviser", advisers[0].DisplayName);
        Assert.Equal(0, source.CallCount);
    }

    private sealed class StubAdviserSourceRepository : IAdviserSourceRepository
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<Adviser>>([]);
        }
    }
}
