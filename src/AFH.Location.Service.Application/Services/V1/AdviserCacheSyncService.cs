using AFH.Location.Service.Application.Abstractions;

namespace AFH.Location.Service.Application.Services.V1;

public sealed class AdviserCacheSyncService : IAdviserCacheSyncService
{
    private readonly IAdviserSourceRepository _sourceRepository;
    private readonly IAdviserReferenceCacheRepository _cacheRepository;

    public AdviserCacheSyncService(
        IAdviserSourceRepository sourceRepository,
        IAdviserReferenceCacheRepository cacheRepository)
    {
        _sourceRepository = sourceRepository;
        _cacheRepository = cacheRepository;
    }

    public async Task<int> SyncAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
    {
        var advisers = await _sourceRepository.GetAllAsync(adviserIds, ct);
        var syncedUtc = DateTime.UtcNow;
        await _cacheRepository.UpsertAsync(advisers, syncedUtc, ct);
        return advisers.Count;
    }
}
