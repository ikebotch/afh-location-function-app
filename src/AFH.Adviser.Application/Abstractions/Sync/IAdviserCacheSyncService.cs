namespace AFH.Adviser.Application.Abstractions.Sync;

using AFH.Adviser.Application.Abstractions.Repositories;

public interface IAdviserCacheSyncService
{
    Task<AdviserReferenceCacheSyncResult> SyncAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
}
