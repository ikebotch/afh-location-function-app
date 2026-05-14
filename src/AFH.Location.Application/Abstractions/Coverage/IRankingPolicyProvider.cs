using AFH.Location.Domain;

namespace AFH.Location.Application.Abstractions.Coverage;

public interface IRankingPolicyProvider
{
    Task<RankingOptions> GetAsync(CancellationToken ct);
}