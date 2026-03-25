namespace AFH.Location.Service.Application.Abstractions;

public interface ISearchAuditRepository
{
    Task SaveAsync(SearchAuditEntry entry, CancellationToken ct);
}

public sealed class SearchAuditEntry
{
    public string RequestId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime RequestedStartUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int SearchHorizonMinutes { get; set; }
    public string? DestinationPostcode { get; set; }
    public string? RegionsCsv { get; set; }
    public int CandidatesReturned { get; set; }
    public string? SelectedAdviserId { get; set; }
    public double? SelectedAdviserRating { get; set; }
    public bool? SelectedAdviserGoldStar { get; set; }
    public int? SelectedTravelMinutes { get; set; }
    public int? SelectedMaxTravelTimeMinutes { get; set; }
    public int? SelectedCompanyBufferMinutes { get; set; }
    public int? SelectedTravelBufferMinutes { get; set; }
    public string? SelectedOriginSource { get; set; }
    public string PayloadJson { get; set; } = "{}";
}
