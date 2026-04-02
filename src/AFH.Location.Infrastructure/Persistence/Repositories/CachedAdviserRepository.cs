using AFH.Location.Application.Abstractions;
using AFH.Location.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class CachedAdviserRepository : IAdviserRepository
{
    private readonly IAdviserReferenceCacheRepository _cacheRepository;
    private readonly IAdviserSourceRepository _sourceRepository;
    private readonly ILogger<CachedAdviserRepository> _logger;

    public CachedAdviserRepository(
        IAdviserReferenceCacheRepository cacheRepository,
        IAdviserSourceRepository sourceRepository,
        ILogger<CachedAdviserRepository> logger)
    {
        _cacheRepository = cacheRepository;
        _sourceRepository = sourceRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
    {
        var cached = await _cacheRepository.GetAllAsync(adviserIds, ct);
        if (cached.Count > 0)
            return cached;

        _logger.LogWarning("Adviser cache miss on hot path. Falling back to live adviser source.");
        var live = await _sourceRepository.GetAllAsync(adviserIds, ct);
        if (live.Count > 0)
            await _cacheRepository.UpsertAsync(live, DateTime.UtcNow, ct);

        return live;
    }
}
