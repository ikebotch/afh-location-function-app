namespace AFH.Location.Service.Core.Services.Common;

public sealed class DestinationResolved
{
    public required double Lat { get; init; }
    public required double Lng { get; init; }
    public required DestinationSource Source { get; init; }
    public string? NormalisedAddress { get; init; }
    public bool IsExact => Source == DestinationSource.Coordinates;
}
