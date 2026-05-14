namespace AFH.Location.Application.Abstractions.Advisers;

public interface IAdviserCacheSyncService
{
    Task<int> SyncAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
}
