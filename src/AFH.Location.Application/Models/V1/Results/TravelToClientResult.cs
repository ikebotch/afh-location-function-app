namespace AFH.Location.Application.Models.V1;

public sealed class TravelToClientResult
{
    public int EtaMinutes { get; set; }
    public double DistanceMiles { get; set; }
    public string Confidence { get; set; } = "Low";
}
