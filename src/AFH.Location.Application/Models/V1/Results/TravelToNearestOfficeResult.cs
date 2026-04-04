namespace AFH.Location.Application.Models.V1;

public sealed class TravelToNearestOfficeResult
{
    public string OfficeId { get; set; } = string.Empty;
    public int EtaMinutes { get; set; }
    public double? DistanceMiles { get; set; }
    public string? Confidence { get; set; }
}
