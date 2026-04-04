namespace AFH.Location.Contract.V1.Responses;

public sealed class AdviserCoverageResponseV1
{
    public IReadOnlyList<AdviserCoveragePointV1> Advisers { get; init; } = [];
    public IReadOnlyList<RegionCoveragePointV1> Regions { get; init; } = [];
}
