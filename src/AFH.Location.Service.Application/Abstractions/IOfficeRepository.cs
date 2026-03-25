using AFH.Location.Service.Domain.Entities;

namespace AFH.Location.Service.Application.Abstractions;

public interface IOfficeRepository
{
    Task<IReadOnlyList<Office>> GetAllAsync(CancellationToken ct);
}