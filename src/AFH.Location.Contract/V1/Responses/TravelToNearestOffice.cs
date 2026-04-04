namespace AFH.Location.Contract.V1.Responses;

public sealed class TravelToNearestOffice
{
    public string OfficeId { get; set; } = "";
    public int EtaMinutes { get; set; }
    public double? DistanceMiles { get; set; }
    public string? Confidence { get; set; }
}
