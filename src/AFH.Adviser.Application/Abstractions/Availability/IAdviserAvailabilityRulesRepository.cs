using AFH.Adviser.Application.Models.Availability;

namespace AFH.Adviser.Application.Abstractions.Availability;

public interface IAdviserAvailabilityRulesRepository
{
    Task<AdviserAvailabilityRules?> GetActiveRulesAsync(string projectContext, CancellationToken ct);
    Task<AvailabilityRuleRecord> CreateRuleAsync(AvailabilityRuleUpsert request, CancellationToken ct);
    Task<AvailabilityRuleRecord?> UpdateRuleAsync(string id, AvailabilityRuleUpsert request, CancellationToken ct);
    Task<bool> DeleteRuleAsync(string id, CancellationToken ct);
}
