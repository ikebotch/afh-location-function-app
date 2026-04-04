using AFH.Location.Domain.Entities;

namespace AFH.Location.Application.Abstractions;

public interface IOfficeRepository
{
    Task<IReadOnlyList<Office>> GetAllAsync(CancellationToken ct);
}