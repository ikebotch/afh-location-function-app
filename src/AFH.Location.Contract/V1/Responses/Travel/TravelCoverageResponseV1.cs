using AFH.Location.Contract.V1.Requests.Travel;

namespace AFH.Location.Contract.V1.Responses.Travel;

public sealed record TravelCoverageResponseV1
{
    public string SourcePostcode { get; init; } = string.Empty;
    public LocationCoordinatesV1? SourceCoordinates { get; init; }
    public TravelCoverageTimeContextV1 TimeContext { get; init; } = new();
    public IReadOnlyList<TravelCoverageDestinationOutcomeV1> Destinations { get; init; } = [];
    public LocationRequestContextV1 RequestContext { get; init; } = new();
}

public sealed record TravelCoverageDestinationOutcomeV1
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public TravelCoverageStatusV1 Status { get; init; }
    public LocationCoordinatesV1? Coordinates { get; init; }
    public TravelRouteOutcomeV1? Route { get; init; }
    public CoverageOutcomeV1? Coverage { get; init; }
    public IReadOnlyList<ApiWarning> Warnings { get; init; } = [];
}

public enum TravelCoverageStatusV1
{
    Succeeded = 0,
    SourcePostcodeUnresolved = 1,
    DestinationPostcodeUnresolved = 2,
    RouteUnavailable = 3,
    Failed = 4
}

public sealed record TravelRouteOutcomeV1
{
    public int TravelTimeMinutes { get; init; }
    public double TravelDistanceMiles { get; init; }
    public string Confidence { get; init; } = string.Empty;
    public TravelRouteResolutionSourceV1 ResolutionSource { get; init; }
}

public enum TravelRouteResolutionSourceV1
{
    Unknown = 0,
    Cache = 1,
    Database = 2,
    AzureMaps = 3
}

public sealed record CoverageOutcomeV1
{
    public bool IsWithinCoverage { get; init; }
    public int? MaxTravelTimeMinutes { get; init; }
    public double? MaxDistanceMiles { get; init; }
}

public sealed record LocationCoordinatesV1
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
