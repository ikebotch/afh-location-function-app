using AFH.Location.Application.Models.V1.Travel;

namespace AFH.Location.Application.Abstractions.Travel;

public interface ITravelCoverageService
{
    Task<TravelCoverageResult> EvaluateAsync(TravelCoverageRequest request, CancellationToken ct);
}
