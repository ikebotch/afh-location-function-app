namespace AFH.Location.Service.Core.Services.Common;


public sealed class BaseOfficePolicy
{
    public Dictionary<string, string> RegionOfficeMap { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string DefaultOfficeId { get; init; } = "OFF-DEFAULT";
}