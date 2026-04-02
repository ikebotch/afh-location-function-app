using AFH.Location.Application.Models.V1;

namespace AFH.Location.Application.Abstractions;

public interface IAdviserCoverageService
{
    Task<AdviserCoverageResult> GetCoverageAsync(CancellationToken ct);
}
