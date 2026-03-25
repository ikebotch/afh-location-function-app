using AFH.Location.Service.Application.Models.V1;

namespace AFH.Location.Service.Application.Abstractions;

public interface IAdviserCoverageService
{
    Task<AdviserCoverageResult> GetCoverageAsync(CancellationToken ct);
}
