namespace AFH.Location.Contract.V1.Responses;

public sealed class LocationSearchBatchItemResponseV1
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public LocationSearchResponseV1? Result { get; set; }
}
