using AFH.Location.Service.Core.Services.Common;

namespace AFH.Location.Service.Core.Abstractions;

public interface IBaseOfficePolicyProvider
{
    Task<BaseOfficePolicy> GetAsync(CancellationToken ct);
}
