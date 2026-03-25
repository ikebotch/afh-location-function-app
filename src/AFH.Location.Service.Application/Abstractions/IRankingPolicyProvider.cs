using AFH.Location.Service.Domain;

namespace AFH.Location.Service.Application.Abstractions;

public interface IRankingPolicyProvider
{
    Task<RankingOptions> GetAsync(CancellationToken ct);
}