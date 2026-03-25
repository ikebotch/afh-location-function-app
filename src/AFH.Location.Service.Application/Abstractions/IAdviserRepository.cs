using AFH.Location.Service.Domain.Entities;

namespace AFH.Location.Service.Application.Abstractions;

public interface IAdviserRepository
{
    Task<IReadOnlyList<Adviser>> GetAllAsync(
     IReadOnlyCollection<string>? adviserIds,
     CancellationToken ct);
}