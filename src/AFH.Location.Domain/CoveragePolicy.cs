namespace AFH.Location.Domain;

public sealed class CoveragePolicy
{
    public double DefaultRadiusMiles { get; set; }
    public int DefaultMaxTravelTimeMinutes { get; set; } = 90;

    // Adviser-based overrides
    public Dictionary<string, double> AdviserRadiusMiles { get; set; } = new();
    public Dictionary<string, int> AdviserMaxTravelTimeMinutes { get; set; } = new();

    // Region-based overrides
    public Dictionary<string, double> RegionRadiusMiles { get; set; } = new();
    public Dictionary<string, int> RegionMaxTravelTimeMinutes { get; set; } = new();

    // Office-based overrides
    public Dictionary<string, double> OfficeRadiusMiles { get; set; } = new();
}
