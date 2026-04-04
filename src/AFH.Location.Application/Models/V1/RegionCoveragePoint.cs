namespace AFH.Location.Application.Models.V1;

public sealed class RegionCoveragePoint
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
