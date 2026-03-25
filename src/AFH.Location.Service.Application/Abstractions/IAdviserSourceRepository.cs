using AFH.Location.Service.Domain.Entities;

namespace AFH.Location.Service.Application.Abstractions;

public interface IAdviserSourceRepository
{
    Task<IReadOnlyList<Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct);
}
