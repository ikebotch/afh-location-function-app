namespace AFH.Location.Application.Models.V1.Batch;

public sealed class LocationSearchBatchResult
{
    public DateTime GeneratedAtUtc { get; set; }
    public List<LocationSearchBatchItemResult> Results { get; set; } = [];
}
