using AFH.Location.Application.Models.V1.Results;

namespace AFH.Location.Application.Models.V1.Batch;

public sealed class LocationSearchBatchItemResult
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public LocationSearchResult? Result { get; set; }
}
