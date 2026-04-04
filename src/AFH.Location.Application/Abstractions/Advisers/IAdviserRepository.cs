using AFH.Location.Domain.Entities;

namespace AFH.Location.Application.Abstractions;

public interface IAdviserRepository
{
    Task<IReadOnlyList<Adviser>> GetAllAsync(
     IReadOnlyCollection<string>? adviserIds,
     CancellationToken ct);
}