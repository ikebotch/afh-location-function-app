using System.Text.Json.Serialization;

namespace AFH.Location.Contract.V1.Requests.Travel;

public sealed record TravelCoverageRequestV1
{
    public string SourcePostcode { get; init; } = string.Empty;
    public TravelCoverageTimeContextV1 TimeContext { get; init; } = new();
    public IReadOnlyList<TravelCoverageDestinationRequestV1> Destinations { get; init; } = [];
    public LocationRequestContextV1 RequestContext { get; init; } = new();
}

public sealed record TravelCoverageTimeContextV1
{
    public TravelEvaluationModeV1 TravelEvaluationMode { get; init; } = TravelEvaluationModeV1.TimeIndependent;
    public SlotResponseModeV1 SlotResponseMode { get; init; } = SlotResponseModeV1.Grouped;
    /// <summary>Window start for slot generation (inclusive).</summary>
    public DateTimeOffset? StartTime { get; init; }
    /// <summary>Window end for slot generation (exclusive).</summary>
    public DateTimeOffset? EndTime { get; init; }
    /// <summary>Duration of each slot in minutes.</summary>
    public int? SearchIntervalMinutes { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TravelEvaluationModeV1
{
    TimeIndependent = 0,
    TimeDependent = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SlotResponseModeV1
{
    Grouped = 0,
    Expanded = 1,
    Summary = 2
}

public sealed record TravelCoverageDestinationRequestV1
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public int? MaxTravelTimeMinutes { get; init; }
    public double? MaxDistanceMiles { get; init; }
}

public sealed record LocationRequestContextV1
{
    public string? CorrelationId { get; init; }
}
