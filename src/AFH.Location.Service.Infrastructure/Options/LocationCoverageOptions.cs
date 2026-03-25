namespace AFH.Location.Service.Infrastructure.Options;

public sealed class LocationCoverageOptions
{
    public const string SectionName = "LocationSearch:Coverage";
    public double AverageTravelSpeedMph { get; set; } = 35d;
}
