namespace AFH.Location.Application.Models.V1.Results;

public sealed class CoverageResult
{
    public bool WithinCoverage { get; set; }
    public string AnchorPostcode { get; set; } = string.Empty;
    public double DistanceMiles { get; set; }
}
