using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Application.Abstractions;

public interface IAdviserSourceRepository
{
    Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
}
