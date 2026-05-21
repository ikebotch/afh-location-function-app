using AFH.Adviser.Application.Abstractions;
using Entities = AFH.Adviser.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AFH.Adviser.Infrastructure.Persistence.Repositories;

public sealed class CachedAdviserRepository : IAdviserRepository
{
    private readonly IAdviserReferenceCacheRepository _cacheRepository;
    private readonly IAdviserSourceRepository _sourceRepository;
    private readonly AdviserSourceRefreshCoordinator _refreshCoordinator;
    private readonly ILogger<CachedAdviserRepository> _logger;

    public CachedAdviserRepository(
        IAdviserReferenceCacheRepository cacheRepository,
        IAdviserSourceRepository sourceRepository,
        AdviserSourceRefreshCoordinator refreshCoordinator,
        ILogger<CachedAdviserRepository> logger)
    {
        _cacheRepository = cacheRepository;
        _sourceRepository = sourceRepository;
        _refreshCoordinator = refreshCoordinator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
    {
        var cached = await _cacheRepository.GetAllAsync(adviserIds, ct);
        if (cached.Count > 0)
            return cached;

        _logger.LogWarning("Entities.Adviser cache miss on hot path. Falling back to live adviser source.");
        if (adviserIds is null || adviserIds.Count == 0)
            return await _refreshCoordinator.RefreshAllAsync(ct);

        var live = await _sourceRepository.GetAllAsync(adviserIds, ct);

        if (live.Count > 0)
            await _cacheRepository.UpsertAsync(live, DateTime.UtcNow, ct);

        return live;
    }
}
