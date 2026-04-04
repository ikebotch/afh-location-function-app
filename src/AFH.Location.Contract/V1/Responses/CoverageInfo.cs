namespace AFH.Location.Contract.V1.Responses;

public sealed class CoverageInfo
{
    public bool WithinCoverage { get; set; }
    public string AnchorPostcode { get; set; } = "";
    public double DistanceMiles { get; set; }
}
