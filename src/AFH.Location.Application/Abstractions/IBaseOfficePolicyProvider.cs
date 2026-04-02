using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions;

public interface IBaseOfficePolicyProvider
{
    Task<BaseOfficePolicy> GetAsync(CancellationToken ct);
}
