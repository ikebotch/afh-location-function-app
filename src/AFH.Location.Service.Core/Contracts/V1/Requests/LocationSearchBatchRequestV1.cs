namespace AFH.Location.Service.Core.Contracts.V1.Requests;

public sealed class LocationSearchBatchRequestV1
{
    public List<LocationSearchRequestV1> Requests { get; set; } = new();
}
