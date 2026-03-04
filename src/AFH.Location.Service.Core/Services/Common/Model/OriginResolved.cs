using AFH.Location.Service.Core.Services.Common.Enums;

namespace AFH.Location.Service.Core.Services.Common.Model;



public sealed class OriginResolved
{
    public required double Lat { get; init; }
    public required double Lng { get; init; }
    public required OriginSource Source { get; init; }

    public string? OfficeId { get; init; }
    public string? CalendarEventId { get; init; }
}