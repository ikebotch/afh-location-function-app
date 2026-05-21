using AFH.Location.Application.Models.Travel;

using AFH.Location.Domain.Travel;

namespace AFH.Location.Application.Abstractions.Travel;

public interface ITravelRouteOutcomeProvider
{
    Task<IReadOnlyDictionary<string, TravelRouteOutcome>> GetOutcomesAsync(
        TravelRouteOutcomeRequest request,
        CancellationToken ct);
}
