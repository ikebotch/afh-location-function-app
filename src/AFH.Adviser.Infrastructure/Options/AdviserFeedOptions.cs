namespace AFH.Adviser.Infrastructure.Options;

public sealed class AdviserFeedOptions
{
    public const string SectionName = "AdviserFeed";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = "/api/v1/mock/advisers";
    public string? ApiKey { get; set; }
}
