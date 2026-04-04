namespace AFH.Location.Application.Abstractions;

public interface IAdviserCacheSyncService
{
    Task<int> SyncAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
}
