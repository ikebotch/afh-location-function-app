using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions.Geo;

public interface IGeoCachePolicyProvider
{
    Task<GeoCachePolicy> GetAsync(CancellationToken ct);
}