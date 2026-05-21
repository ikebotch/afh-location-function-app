using AFH.Adviser.Application.Abstractions;
using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Infrastructure.Persistence.Repositories;

public sealed class InMemoryAdviserReferenceCacheRepository : IAdviserReferenceCacheRepository
{
    private readonly Dictionary<string, Entities.Adviser> _items = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
    {
        if (adviserIds is null || adviserIds.Count == 0)
            return Task.FromResult<IReadOnlyList<Entities.Adviser>>(_items.Values.ToList());

        var ids = adviserIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyList<Entities.Adviser>>(_items.Values.Where(x => ids.Contains(x.AdviserId)).ToList());
    }

    public Task UpsertAsync(IReadOnlyCollection<Entities.Adviser> advisers, DateTime syncedUtc, CancellationToken ct)
    {
        foreach (var adviser in advisers)
        {
            adviser.LastSyncedUtc = syncedUtc;
            _items[adviser.AdviserId] = adviser;
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasDataAsync(CancellationToken ct) => Task.FromResult(_items.Count > 0);
}
