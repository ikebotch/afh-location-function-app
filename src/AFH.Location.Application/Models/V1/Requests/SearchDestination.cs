namespace AFH.Location.Application.Models.V1.Requests;

public sealed class SearchDestination
{
    public SearchCoordinates? Coordinates { get; set; }
    public SearchAddress? Address { get; set; }
}
