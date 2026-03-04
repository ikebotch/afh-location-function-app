namespace AFH.Location.Service.Core.Services.Common;

public sealed class CoveragePolicy
{
    public double DefaultRadiusMiles { get; set; }

    // Adviser-based overrides
    public Dictionary<string, double> AdviserRadiusMiles { get; set; } = new();

    // Region-based overrides
    public Dictionary<string, double> RegionRadiusMiles { get; set; } = new();

    // Office-based overrides
    public Dictionary<string, double> OfficeRadiusMiles { get; set; } = new();
}