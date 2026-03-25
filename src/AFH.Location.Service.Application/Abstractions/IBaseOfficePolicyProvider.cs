using AFH.Location.Service.Domain;

namespace AFH.Location.Service.Application.Abstractions;

public interface IBaseOfficePolicyProvider
{
    Task<BaseOfficePolicy> GetAsync(CancellationToken ct);
}
