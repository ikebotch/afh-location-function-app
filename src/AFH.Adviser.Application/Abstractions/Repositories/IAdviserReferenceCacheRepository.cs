using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Application.Abstractions.Repositories;

public interface IAdviserReferenceCacheRepository
{
    Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
    Task<AdviserReferenceCacheSyncResult> UpsertAsync(IReadOnlyCollection<Entities.Adviser> advisers, DateTime syncedUtc, CancellationToken ct);
    Task<bool> HasDataAsync(CancellationToken ct);
    Task<bool> SetActiveAsync(string adviserId, bool isActive, CancellationToken ct);
    Task<bool> DeleteAsync(string adviserId, CancellationToken ct);
}

public sealed record AdviserReferenceCacheSyncResult(
    int Synced,
    int Created,
    int Updated,
    int Unchanged);
