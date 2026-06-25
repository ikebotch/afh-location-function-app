using AFH.Adviser.Application.Models.Availability;

namespace AFH.Adviser.Application.Abstractions.Availability;

public interface IAdviserAvailabilityRulesService
{
    Task<AdviserAvailabilityRules?> GetActiveRulesAsync(string? projectContext, CancellationToken ct);
}
