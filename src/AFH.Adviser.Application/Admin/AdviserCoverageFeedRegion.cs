namespace AFH.Adviser.Application.Admin;

public sealed class AdviserCoverageFeedRegion
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
