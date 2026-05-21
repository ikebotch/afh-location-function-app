using AFH.Location.Domain.Travel;

namespace AFH.Location.Application.Models.Travel;

public sealed record TravelCoverageRequest
{
    public string SourcePostcode { get; init; } = string.Empty;
    public TravelCoverageTimeContext TimeContext { get; init; } = new();
    public IReadOnlyList<TravelCoverageDestinationRequest> Destinations { get; init; } = [];
    public TravelCoverageRequestMetadata Metadata { get; init; } = new();
    public LocationRequestContext RequestContext { get; init; } = new();
}

public enum TravelCoverageSlotResponseMode
{
    Grouped = 0,
    Expanded = 1,
    Summary = 2
}

public sealed record TravelCoverageTimeContext
{
    public DateTimeOffset? RequestedDepartureTime { get; init; }
    public TravelCoverageTimingMode TimingMode { get; init; } = TravelCoverageTimingMode.TimeIndependent;
    public TravelCoverageSlotResponseMode SlotResponseMode { get; init; } = TravelCoverageSlotResponseMode.Grouped;
    /// <summary>Window start for slot generation (inclusive).</summary>
    public DateTimeOffset? StartTime { get; init; }
    /// <summary>Window end for slot generation (exclusive).</summary>
    public DateTimeOffset? EndTime { get; init; }
    /// <summary>Duration of each slot in minutes. When omitted the full window is treated as a single slot.</summary>
    public int? SearchIntervalMinutes { get; init; }
}

public sealed record TravelCoverageSlotOutcome
{
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public TravelRouteOutcome? Route { get; init; }
    public TravelCoverageOutcome? Coverage { get; init; }
}

public sealed record TravelCoverageDestinationRequest
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public int? MaxTravelTimeMinutes { get; init; }
    public double? MaxDistanceMiles { get; init; }
}

public sealed record TravelCoverageRequestMetadata
{
    public string? AppointmentType { get; init; }
    public string? Channel { get; init; }
}

public sealed record LocationRequestContext
{
    public string? CorrelationId { get; init; }
    public string? RequestedBy { get; init; }
}

public sealed record TravelCoverageResult
{
    public string SourcePostcode { get; init; } = string.Empty;
    public LocationCoordinates? SourceCoordinates { get; init; }
    public TravelCoverageTimeContext TimeContext { get; init; } = new();
    public IReadOnlyList<TravelCoverageDestinationOutcome> Destinations { get; init; } = [];
    public LocationRequestContext RequestContext { get; init; } = new();
}

public sealed record TravelCoverageDestinationOutcome
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public TravelCoverageStatus Status { get; init; }
    public LocationCoordinates? Coordinates { get; init; }
    public TravelRouteOutcome? Route { get; init; }
    public TravelCoverageOutcome? Coverage { get; init; }
    public IReadOnlyList<TravelCoverageSlotOutcome> Slots { get; init; } = [];
    public IReadOnlyList<TravelCoveragePresentedSlot>? PresentedSlots { get; init; }
    public IReadOnlyList<TravelCoverageWarning> Warnings { get; init; } = [];
}

public enum TravelCoverageStatus
{
    Succeeded = 0,
    SourcePostcodeUnresolved = 1,
    DestinationPostcodeUnresolved = 2,
    RouteUnavailable = 3,
    Failed = 4
}

public sealed record TravelCoverageOutcome
{
    public bool IsWithinCoverage { get; init; }
    public int? MaxTravelTimeMinutes { get; init; }
    public double? MaxDistanceMiles { get; init; }
}

public sealed record LocationCoordinates(double Latitude, double Longitude);

public sealed record TravelCoverageWarning(string Code, string Message);

public sealed record PostcodeCoordinateResolution
{
    public string Postcode { get; init; } = string.Empty;
    public LocationCoordinates? Coordinates { get; init; }
    public bool Succeeded => Coordinates is not null;
}

public sealed record TravelRouteOutcomeRequest
{
    public LocationCoordinates Source { get; init; } = new(0, 0);
    public IReadOnlyDictionary<string, LocationCoordinates> Destinations { get; init; } =
        new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase);
    public TravelCoverageTimeContext TimeContext { get; init; } = new();
}

public sealed record RouteTimeRequest
{
    public string? CorrelationId { get; init; }
    public DateTimeOffset DepartAt { get; init; }
    public LocationCoordinates Source { get; init; } = new(0, 0);
    public LocationCoordinates Destination { get; init; } = new(0, 0);
}

public sealed record RouteTimeResult
{
    public string? CorrelationId { get; init; }
    public int? TravelTimeMinutes { get; init; }
    public double? TravelDistanceMiles { get; init; }
    public RouteTimeStatus Status { get; init; }
    public IReadOnlyList<TravelCoverageWarning> Warnings { get; init; } = [];
}

public enum RouteTimeStatus
{
    Succeeded = 0,
    RouteUnavailable = 1,
    Failed = 2
}
