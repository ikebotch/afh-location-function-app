namespace AFH.Adviser.Application.Models.Feed;

public sealed class AdviserFeedRegionItem
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
