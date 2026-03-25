namespace AFH.Location.Service.Domain;


public sealed class BaseOfficePolicy
{
    public Dictionary<string, string> RegionOfficeMap { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string DefaultOfficeId { get; init; } = "OFF-DEFAULT";
}