using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions;

public interface IRankingPolicyProvider
{
    Task<RankingOptions> GetAsync(CancellationToken ct);
}