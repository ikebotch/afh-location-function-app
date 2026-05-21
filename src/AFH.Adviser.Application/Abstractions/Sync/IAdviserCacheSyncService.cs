namespace AFH.Adviser.Application.Abstractions.Sync;

public interface IAdviserCacheSyncService
{
    Task<int> SyncAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
}
