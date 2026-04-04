namespace AFH.Location.Contract.V1.Responses;

public sealed class LocationSearchBatchResponseV1
{
    public DateTime GeneratedAtUtc { get; set; }
    public List<LocationSearchBatchItemResponseV1> Results { get; set; } = new();
}
