namespace AFH.Location.Domain;

public sealed class RankingOptions
{
    // Availability penalties
    public double AvailablePenalty { get; set; } = 0;
    public double AvailableLaterPenalty { get; set; } = 20;
    public double BusyPenalty { get; set; } = 100;

    // Travel weights
    public double EtaMinuteWeight { get; set; } = 1.0;
    public double DistanceMileWeight { get; set; } = 0.5;

    // Bonuses / penalties
    public double PreferredBonus { get; set; } = 10;          // subtract from score
    public double LowConfidencePenalty { get; set; } = 15;

    // Bias weights
    public double RegionMatchBonus { get; set; } = 5;          // subtract from score
    public double CoverageFailPenalty { get; set; } = 250;
    public double RatingWeight { get; set; } = 5;
    public double DefaultMinAdviserRating { get; set; } = 0;
    public double? MaxEligibleScore { get; set; } = null;
}
