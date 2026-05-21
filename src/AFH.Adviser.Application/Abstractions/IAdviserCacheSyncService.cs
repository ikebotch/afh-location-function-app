namespace AFH.Adviser.Application.Abstractions;

public interface IAdviserCacheSyncService
{
    Task<int> SyncAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
}
