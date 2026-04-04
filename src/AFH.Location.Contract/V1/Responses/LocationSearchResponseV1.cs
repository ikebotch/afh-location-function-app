namespace AFH.Location.Contract.V1.Responses;

public sealed class LocationSearchResponseV1
{
    public string RequestId { get; set; } = default!;
    public DateTime GeneratedAtUtc { get; set; }
    public List<LocationCandidate> Candidates { get; set; } = new();
    public List<ApiWarning> Warnings { get; set; } = new();
}
