namespace AFH.Adviser.Application.Admin;

public sealed class AdviserCoverageFeedAdviser
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string MailboxUserId { get; init; } = string.Empty;
    public string? Region { get; init; }
    public string? Postcode { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<string> Skills { get; init; } = [];
    public double Rating { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int MaxTravelTimeMinutes { get; init; }
    public double RadiusMiles { get; init; }
    public int RadiusKm { get; init; }
    public string RadiusSource { get; init; } = default!;
}
