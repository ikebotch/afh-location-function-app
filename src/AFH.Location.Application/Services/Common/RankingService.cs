using AFH.Location.Application.Models.V1.Results;
using AFH.Location.Domain;

namespace AFH.Location.Application.Services.Common;

public sealed class RankingService
{
    public RankedCandidate Rank(LocationSearchCandidate c, RankingOptions opt)
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

        if (c.TravelToClient.EtaMinutes.HasValue && c.TravelToClient.EtaMinutes.Value > 0)
        {
            score += c.TravelToClient.EtaMinutes.Value * opt.EtaMinuteWeight;
            reasons.Add($"RANK_ETA_{c.TravelToClient.EtaMinutes.Value}");
        }
        else
        {
            reasons.Add("RANK_ETA_UNVERIFIED");
        }

        if (c.TravelToClient.DistanceMiles.HasValue && c.TravelToClient.DistanceMiles.Value > 0)
        {
            score += c.TravelToClient.DistanceMiles.Value * opt.DistanceMileWeight;
            reasons.Add($"RANK_DISTANCE_{c.TravelToClient.DistanceMiles.Value}");
        }
        else
        {
            reasons.Add("RANK_DISTANCE_UNVERIFIED");
        }

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