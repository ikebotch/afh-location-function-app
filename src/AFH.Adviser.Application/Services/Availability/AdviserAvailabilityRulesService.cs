using AFH.Adviser.Application.Abstractions.Availability;
using AFH.Adviser.Application.Models.Availability;

namespace AFH.Adviser.Application.Services.Availability;

public sealed class AdviserAvailabilityRulesService : IAdviserAvailabilityRulesService
{
    private readonly IAdviserAvailabilityRulesRepository _repository;

    public AdviserAvailabilityRulesService(IAdviserAvailabilityRulesRepository repository)
    {
        _repository = repository;
    }

    public Task<AdviserAvailabilityRules?> GetActiveRulesAsync(string? projectContext, CancellationToken ct)
    {
        var context = string.IsNullOrWhiteSpace(projectContext) ? "Booking" : projectContext.Trim();
        return _repository.GetActiveRulesAsync(context, ct);
    }
}
