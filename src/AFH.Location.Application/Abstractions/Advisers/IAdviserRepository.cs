using AFH.Location.Domain.Entities;

namespace AFH.Location.Application.Abstractions.Advisers;

public interface IAdviserRepository
{
    Task<IReadOnlyList<Adviser>> GetAllAsync(
     IReadOnlyCollection<string>? adviserIds,
     CancellationToken ct);
}