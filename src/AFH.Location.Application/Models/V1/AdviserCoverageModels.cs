namespace AFH.Location.Application.Models.V1;

public sealed class AdviserCoverageResult
{
    public IReadOnlyList<AdviserCoveragePoint> Advisers { get; init; } = [];
    public IReadOnlyList<RegionCoveragePoint> Regions { get; init; } = [];
}
