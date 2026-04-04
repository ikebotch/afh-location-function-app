namespace AFH.Location.Contract.V1.Requests;

public sealed class Destination
{
    public LatLng? Coordinates { get; set; }
    public Address? Address { get; set; }
}
