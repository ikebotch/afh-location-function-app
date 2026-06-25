using AFH.Adviser.Application.Models.Availability;

namespace AFH.Adviser.Application.Abstractions.Availability;

public interface IAdviserAvailabilityRulesRepository
{
    Task<AdviserAvailabilityRules?> GetActiveRulesAsync(string projectContext, CancellationToken ct);
}
