namespace AFH.Location.Service.Core.Contracts.V1.Responses;

public sealed class AdviserCoverageResponseV1
{
    public IReadOnlyList<AdviserCoveragePointV1> Advisers { get; init; } = [];
    public IReadOnlyList<RegionCoveragePointV1> Regions { get; init; } = [];
}

public sealed class AdviserCoveragePointV1
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

public sealed class RegionCoveragePointV1
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
