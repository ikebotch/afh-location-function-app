using AFH.Location.Service.Core.Services.Common;

namespace AFH.Location.Service.Core.Abstractions;

public interface IGeoCachePolicyProvider
{
    Task<GeoCachePolicy> GetAsync(CancellationToken ct);
}