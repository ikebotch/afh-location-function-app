using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Application.Abstractions;

public interface IAdviserReferenceCacheRepository
{
    Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
    Task UpsertAsync(IReadOnlyCollection<Entities.Adviser> advisers, DateTime syncedUtc, CancellationToken ct);
    Task<bool> HasDataAsync(CancellationToken ct);
}
