namespace AFH.Location.Service.Contract.V1.Responses;

public sealed class LocationSearchBatchResponseV1
{
    public DateTime GeneratedAtUtc { get; set; }
    public List<LocationSearchBatchItemResponseV1> Results { get; set; } = new();
}

public sealed class LocationSearchBatchItemResponseV1
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public LocationSearchResponseV1? Result { get; set; }
}
