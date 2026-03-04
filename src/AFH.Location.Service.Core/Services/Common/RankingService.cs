using AFH.Location.Service.Core.Contracts.V1.Responses;

namespace AFH.Location.Service.Core.Services.Common;

public sealed class RankingService
{
    public RankedCandidate Rank(LocationCandidate c, RankingOptions opt)
    {
        double score = 0;
        var reasons = new List<string>();

        if (!c.Coverage.WithinCoverage)
        {
            score += opt.CoverageFailPenalty;
            reasons.Add("RANK_COVERAGE_FAIL");
        }

        switch (c.Availability)
        {
            case "Available":
                score += opt.AvailablePenalty;
                reasons.Add("RANK_AVAILABLE");
                break;
            case "AvailableLater":
                score += opt.AvailableLaterPenalty;
                reasons.Add("RANK_AVAILABLE_LATER");
                break;
            default:
                score += opt.BusyPenalty;
                reasons.Add("RANK_BUSY");
                break;
        }

        score += c.TravelToClient.EtaMinutes * opt.EtaMinuteWeight;
        reasons.Add($"RANK_ETA_{c.TravelToClient.EtaMinutes}");

        score += c.TravelToClient.DistanceMiles * opt.DistanceMileWeight;
        reasons.Add($"RANK_DISTANCE_{c.TravelToClient.DistanceMiles}");

        if (c.Preferred)
        {
            score -= opt.PreferredBonus;
            reasons.Add("RANK_PREFERRED");
        }

        if (c.AdviserRating > 0)
        {
            score -= c.AdviserRating * opt.RatingWeight;
            reasons.Add($"RANK_RATING_{c.AdviserRating:0.##}");
        }

        if (string.Equals(c.TravelToClient.Confidence, "Low", StringComparison.OrdinalIgnoreCase))
        {
            score += opt.LowConfidencePenalty;
            reasons.Add("RANK_LOW_CONFIDENCE");
        }

      
        return new RankedCandidate
        {
            Candidate = c,
            Score = Math.Round(score, 2),
            RankingReasons = reasons
        };
    }
}

public sealed class RankedCandidate
{
    public LocationCandidate Candidate { get; init; } = default!;
    public double Score { get; init; }
    public IReadOnlyList<string> RankingReasons { get; init; } = Array.Empty<string>();
}
