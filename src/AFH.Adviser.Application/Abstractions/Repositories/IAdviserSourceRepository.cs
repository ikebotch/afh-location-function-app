using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Application.Abstractions.Repositories;

public interface IAdviserSourceRepository
{
    Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
}
