using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Domain;
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

        return Task.FromResult(new AvailabilityPolicy
        {
            DefaultBufferMinutes = Math.Max(0, defaultBuffer),
            MaxBufferMinutes = Math.Max(0, maxBuffer)
        });
    }
}
