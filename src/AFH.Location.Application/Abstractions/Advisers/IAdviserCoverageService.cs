using AFH.Location.Application.Models.V1;

namespace AFH.Location.Application.Abstractions.Advisers;

public interface IAdviserCoverageService
{
    Task<AdviserCoverageResult> GetCoverageAsync(CancellationToken ct);
}
