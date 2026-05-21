namespace AFH.Adviser.Application.Models.Feed;

public sealed class AdviserFeedResult
{
    public IReadOnlyList<AdviserFeedItem> Advisers { get; init; } = Array.Empty<AdviserFeedItem>();
    public IReadOnlyList<AdviserFeedRegionItem> Regions { get; init; } = Array.Empty<AdviserFeedRegionItem>();
}
