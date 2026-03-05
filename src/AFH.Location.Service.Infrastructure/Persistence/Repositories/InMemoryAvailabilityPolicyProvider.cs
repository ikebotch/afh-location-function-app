using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Services.Common;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

public sealed class InMemoryAvailabilityPolicyProvider : IAvailabilityPolicyProvider
{
    private readonly IConfiguration _configuration;

    public InMemoryAvailabilityPolicyProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<AvailabilityPolicy> GetAsync(CancellationToken ct)
    {
        var defaultBuffer = _configuration.GetValue<int?>("LocationSearch:Availability:DefaultBufferMinutes") ?? 0;
        var maxBuffer = _configuration.GetValue<int?>("LocationSearch:Availability:MaxBufferMinutes") ?? 180;
        var defaultCompanyBuffer = _configuration.GetValue<int?>("LocationSearch:Availability:DefaultCompanyBufferMinutes") ?? 30;
        var maxCompanyBuffer = _configuration.GetValue<int?>("LocationSearch:Availability:MaxCompanyBufferMinutes") ?? 180;
        var previousClientProximity = _configuration.GetValue<int?>("LocationSearch:Availability:PreviousClientProximityMinutes") ?? 180;
        var requireCalendarAvailability = _configuration.GetValue<bool?>("LocationSearch:Availability:RequireCalendarAvailability") ?? true;

        return Task.FromResult(new AvailabilityPolicy
        {
            DefaultBufferMinutes = Math.Max(0, defaultBuffer),
            MaxBufferMinutes = Math.Max(0, maxBuffer),
            DefaultCompanyBufferMinutes = Math.Max(0, defaultCompanyBuffer),
            MaxCompanyBufferMinutes = Math.Max(0, maxCompanyBuffer),
            PreviousClientProximityMinutes = Math.Max(0, previousClientProximity),
            RequireCalendarAvailability = requireCalendarAvailability
        });
    }
}
