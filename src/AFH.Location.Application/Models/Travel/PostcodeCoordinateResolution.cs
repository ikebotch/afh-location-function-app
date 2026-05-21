namespace AFH.Location.Application.Models.Travel;

public sealed record PostcodeCoordinateResolution
{
    public string Postcode { get; init; } = string.Empty;
    public LocationCoordinates? Coordinates { get; init; }
    public bool Succeeded => Coordinates is not null;
}
