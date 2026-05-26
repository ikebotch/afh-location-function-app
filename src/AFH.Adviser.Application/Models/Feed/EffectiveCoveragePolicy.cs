namespace AFH.Adviser.Application.Models.Feed;

public sealed record EffectiveCoveragePolicy(
    double? RadiusMiles,
    int? MaxTravelTimeMinutes,
    string RadiusSource);
