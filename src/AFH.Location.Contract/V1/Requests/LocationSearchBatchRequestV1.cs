namespace AFH.Location.Contract.V1.Requests;

public sealed class LocationSearchBatchRequestV1
{
    public List<LocationSearchRequestV1> Requests { get; set; } = new();
}
