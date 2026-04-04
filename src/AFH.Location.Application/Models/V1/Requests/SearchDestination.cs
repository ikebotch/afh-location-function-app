namespace AFH.Location.Application.Models.V1;

public sealed class SearchDestination
{
    public SearchCoordinates? Coordinates { get; set; }
    public SearchAddress? Address { get; set; }
}
