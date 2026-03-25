namespace AFH.Location.Service.Contract.V1.Requests;

public sealed class LocationSearchRequestV1
{
    public string RequestId { get; set; } = default!;
    public MeetingWindow Meeting { get; set; } = new();
    public Destination Destination { get; set; } = new();


    public LocationSearchFilters? Filters { get; init; }
}

public sealed class MeetingWindow
{
    public DateTime RequestedStartUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int SearchHorizonMinutes { get; set; }
}

public sealed class Destination
{
    public LatLng? Coordinates { get; set; }
    public Address? Address { get; set; }
}

public sealed class LatLng
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public sealed class Address
{
    public string Line1 { get; set; } = default!;
    public string? Line2 { get; set; }
    public string Town { get; set; } = default!;
    public string Postcode { get; set; } = default!;
    public string Country { get; set; } = "UK";
}

public sealed class Filters
{
    public List<string>? Regions { get; set; }
    public List<string>? RequiredSkills { get; set; }
    public List<string>? PreferredAdviserIds { get; set; }
    public List<string>? ExcludeAdviserIds { get; set; }
}