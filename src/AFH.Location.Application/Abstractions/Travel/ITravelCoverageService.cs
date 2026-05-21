using AFH.Location.Application.Models.Travel;


namespace AFH.Location.Application.Abstractions.Travel;

public interface ITravelCoverageService
{
    Task<TravelCoverageResult> EvaluateAsync(TravelCoverageRequest request, CancellationToken ct);
}
