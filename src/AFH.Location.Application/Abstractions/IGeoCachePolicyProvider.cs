using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions;

public interface IGeoCachePolicyProvider
{
    Task<GeoCachePolicy> GetAsync(CancellationToken ct);
}