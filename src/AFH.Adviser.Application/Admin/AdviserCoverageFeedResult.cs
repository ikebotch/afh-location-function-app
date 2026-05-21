namespace AFH.Adviser.Application.Admin;

public sealed class AdviserCoverageFeedResult
{
    public IReadOnlyList<AdviserCoverageFeedAdviser> Advisers { get; init; } = [];
    public IReadOnlyList<AdviserCoverageFeedRegion> Regions { get; init; } = [];
}
