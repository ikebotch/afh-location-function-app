namespace AFH.Location.Application.Models.V1;

public sealed class AdviserCoverageResult
{
    public IReadOnlyList<AdviserCoveragePoint> Advisers { get; init; } = [];
    public IReadOnlyList<RegionCoveragePoint> Regions { get; init; } = [];
}

public sealed class AdviserCoveragePoint
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Region { get; init; }
    public string? Postcode { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int MaxTravelTimeMinutes { get; init; }
    public double RadiusMiles { get; init; }
    public int RadiusKm { get; init; }
    public string RadiusSource { get; init; } = default!;
}

public sealed class RegionCoveragePoint
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
