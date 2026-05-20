using AFH.Location.Contract.V1.Requests.Travel;

namespace AFH.Location.Contract.V1.Responses.Travel;

/// <summary>Top-level travel coverage response returned by the API.</summary>
public sealed record TravelCoverageResponseV1
{
    public string SourcePostcode { get; init; } = string.Empty;
    public TravelCoverageTimeContextV1 TimeContext { get; init; } = new();
    public IReadOnlyList<TravelCoverageDestinationOutcomeV1> Destinations { get; init; } = [];
    public LocationRequestContextV1 RequestContext { get; init; } = new();
}

/// <summary>
/// Lean per-destination outcome. Rich internal data (coordinates, confidence,
/// resolutionSource) is retained in the application layer for persistence/audit
/// but is not returned to callers.
/// </summary>
public sealed record TravelCoverageDestinationOutcomeV1
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public TravelCoverageStatusV1 Status { get; init; }

    /// <summary>
    /// One entry per time-window slot. Present only when Status == Succeeded.
    /// Each slot carries the travel metrics for that window; for
    /// TimeIndependent routes all slots share the same travel values.
    /// </summary>
    public IReadOnlyList<TravelCoverageSlotV1>? Slots { get; init; }

    /// <summary>
    /// Present only on failure statuses to explain what went wrong.
    /// Omitted (null) on Succeeded outcomes.
    /// </summary>
    public IReadOnlyList<ApiWarning>? Warnings { get; init; }
}

/// <summary>A single time-window slot with travel and coverage metrics.</summary>
public sealed record TravelCoverageSlotV1
{
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public int TravelTimeMinutes { get; init; }
    public double TravelDistanceMiles { get; init; }
    public bool IsWithinCoverage { get; init; }
}

public enum TravelCoverageStatusV1
{
    Succeeded = 0,
    SourcePostcodeUnresolved = 1,
    DestinationPostcodeUnresolved = 2,
    RouteUnavailable = 3,
    Failed = 4
}
