namespace AFH.Location.Application.Models.V1;

public sealed class LocationSearchResult
{
    public string RequestId { get; set; } = default!;
    public DateTime GeneratedAtUtc { get; set; }
    public List<LocationSearchCandidate> Candidates { get; set; } = [];
    public List<LocationSearchWarning> Warnings { get; set; } = [];
}
