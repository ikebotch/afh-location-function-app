namespace AFH.Location.Service.Infrastructure.Options;

public sealed class GoogleMapsOptions
{
    public const string SectionName = "Maps:Google";

    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
}
