using AFH.Location.Service.Domain;

namespace AFH.Location.Service.Application.Abstractions;

public interface IAvailabilityPolicyProvider
{
    Task<AvailabilityPolicy> GetAsync(CancellationToken ct);
}
