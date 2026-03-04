using AFH.Location.Service.Core.Models;

namespace AFH.Location.Service.Core.Abstractions;

public interface IOfficeRepository
{
    Task<IReadOnlyList<Office>> GetAllAsync(CancellationToken ct);
}