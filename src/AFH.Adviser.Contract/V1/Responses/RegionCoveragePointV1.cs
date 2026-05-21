namespace AFH.Adviser.Contract.V1.Responses;

public sealed class RegionCoveragePointV1
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
