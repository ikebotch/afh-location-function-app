using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions.Coverage;

public interface IBaseOfficePolicyProvider
{
    Task<BaseOfficePolicy> GetAsync(CancellationToken ct);
}
