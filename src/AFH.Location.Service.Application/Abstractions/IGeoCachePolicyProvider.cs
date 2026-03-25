using AFH.Location.Service.Domain;

namespace AFH.Location.Service.Application.Abstractions;

public interface IGeoCachePolicyProvider
{
    Task<GeoCachePolicy> GetAsync(CancellationToken ct);
}