using AFH.Location.Service.Core.Domain.Entities;

namespace AFH.Location.Service.Core.Abstractions;

public interface IAdviserRepository
{
    Task<IReadOnlyList<Adviser>> GetAllAsync(
     IReadOnlyCollection<string>? adviserIds,
     CancellationToken ct);
}