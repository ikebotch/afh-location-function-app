namespace AFH.Location.Contract.V1.Requests.Travel;

public sealed record TravelCoverageRequestV1
{
    public string SourcePostcode { get; init; } = string.Empty;
    public TravelCoverageTimeContextV1 TimeContext { get; init; } = new();
    public IReadOnlyList<TravelCoverageDestinationRequestV1> Destinations { get; init; } = [];
    public TravelCoverageRequestMetadataV1 Metadata { get; init; } = new();
    public LocationRequestContextV1 RequestContext { get; init; } = new();
}

public sealed record TravelCoverageTimeContextV1
{
    public DateTimeOffset? RequestedDepartureTime { get; init; }
    public TravelCoverageTimingModeV1 TimingMode { get; init; } = TravelCoverageTimingModeV1.TimeIndependent;
    /// <summary>Window start for slot generation (inclusive).</summary>
    public DateTimeOffset? StartTime { get; init; }
    /// <summary>Window end for slot generation (exclusive).</summary>
    public DateTimeOffset? EndTime { get; init; }
    /// <summary>Duration of each slot in minutes. When omitted the full window is treated as a single slot.</summary>
    public int? SearchIntervalMinutes { get; init; }
}

public enum TravelCoverageTimingModeV1
{
    TimeIndependent = 0,
    DepartureTime = 1
}

public sealed record TravelCoverageDestinationRequestV1
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public int? MaxTravelTimeMinutes { get; init; }
    public double? MaxDistanceMiles { get; init; }
}

public sealed record TravelCoverageRequestMetadataV1
{
    public string? AppointmentType { get; init; }
    public string? Channel { get; init; }
}

public sealed record LocationRequestContextV1
{
    public string? CorrelationId { get; init; }
    public string? RequestedBy { get; init; }
}
