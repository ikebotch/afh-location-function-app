using AFH.Location.Service.Core.Services.Common;

namespace AFH.Location.Service.Core.Abstractions;

public interface IAvailabilityPolicyProvider
{
    Task<AvailabilityPolicy> GetAsync(CancellationToken ct);
}
