using AFH.Location.Service.Domain.Entities;

namespace AFH.Location.Service.Application.Abstractions;

public interface IAdviserReferenceCacheRepository
{
    Task<IReadOnlyList<Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
    Task UpsertAsync(IReadOnlyCollection<Adviser> advisers, DateTime syncedUtc, CancellationToken ct);
    Task<bool> HasDataAsync(CancellationToken ct);
}
