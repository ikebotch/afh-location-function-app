using AFH.Adviser.Application.Models.Availability;

namespace AFH.Adviser.Application.Abstractions.Availability;

public interface IAdviserAvailabilityRulesService
{
    Task<AdviserAvailabilityRules?> GetActiveRulesAsync(string? projectContext, CancellationToken ct);
    Task<AdviserAvailabilityRules> GetAdminRulesAsync(string? projectContext, string? adviserId, CancellationToken ct);
    Task<AvailabilityTimeSlotsResult> GetAdminTimeSlotsAsync(AvailabilityTimeSlotsQuery query, CancellationToken ct);
}
