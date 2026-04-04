using AFH.Location.Application.Models.V1;

namespace AFH.Location.Application.Services.Common;

public sealed class RankedCandidate
{
    public LocationSearchCandidate Candidate { get; init; } = default!;
    public double Score { get; init; }
    public IReadOnlyList<string> RankingReasons { get; init; } = Array.Empty<string>();
}
