using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions.Coverage;

public interface IAvailabilityPolicyProvider
{
    Task<AvailabilityPolicy> GetAsync(CancellationToken ct);
}
