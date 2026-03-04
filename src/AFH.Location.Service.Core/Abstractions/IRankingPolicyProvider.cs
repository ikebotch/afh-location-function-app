using AFH.Location.Service.Core.Services.Common;

namespace AFH.Location.Service.Core.Abstractions;

public interface IRankingPolicyProvider
{
    Task<RankingOptions> GetAsync(CancellationToken ct);
}