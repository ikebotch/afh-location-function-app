using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions;

public interface IAvailabilityPolicyProvider
{
    Task<AvailabilityPolicy> GetAsync(CancellationToken ct);
}
