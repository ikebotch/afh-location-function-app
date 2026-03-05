namespace AFH.Location.Service.Infrastructure.Options;

public sealed class CalendarServiceOptions
{
    public const string SectionName = "CalendarService";

    public string BaseUrl { get; set; } = string.Empty;
    public string? FunctionKey { get; set; }
    public int ScheduleLookbackMinutes { get; set; } = 360;
}
