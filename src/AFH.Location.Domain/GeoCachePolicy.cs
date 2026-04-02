namespace AFH.Location.Domain;

public sealed class GeoCachePolicy
{
    // Successful geocodes
    public TimeSpan AdviserHomeTtl { get; set; }
    public TimeSpan AdviserOfficeTtl { get; set; }
    public TimeSpan DestinationTtl { get; set; }

    // Failed / zero-result geocodes (shorter TTL)
    public TimeSpan FailureTtl { get; set; }
    public TimeSpan SuccessTtl { get; set; }
}