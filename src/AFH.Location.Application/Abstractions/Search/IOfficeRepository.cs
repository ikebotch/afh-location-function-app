using AFH.Location.Domain.Entities;

namespace AFH.Location.Application.Abstractions.Search;

public interface IOfficeRepository
{
    Task<IReadOnlyList<Office>> GetAllAsync(CancellationToken ct);
}